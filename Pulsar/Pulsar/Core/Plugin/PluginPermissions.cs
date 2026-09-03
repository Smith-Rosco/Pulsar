using System;
using System.Collections.Generic;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// Well-known external-plugin permission tokens. Unknown tokens are treated
    /// as denied so a typo or a future permission can never silently grant
    /// capabilities the host does not understand.
    /// </summary>
    public static class PluginPermissions
    {
        public const string ClipboardRead = "clipboard.read";
        public const string ClipboardWrite = "clipboard.write";
        public const string InputInject = "input.inject";
        public const string WindowFocus = "window.focus";
        public const string ProcessLaunch = "process.launch";
        public const string FileSystemRead = "filesystem.read";
        public const string FileSystemWrite = "filesystem.write";
        public const string NetworkClient = "network.client";
        public const string UiRender = "ui.render";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            ClipboardRead,
            ClipboardWrite,
            InputInject,
            WindowFocus,
            ProcessLaunch,
            FileSystemRead,
            FileSystemWrite,
            NetworkClient,
            UiRender
        };

        public static bool IsKnown(string permission)
        {
            foreach (var known in All)
            {
                if (string.Equals(known, permission, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public interface IPluginPermissionService
    {
        PluginPermissionEvaluation Evaluate(
            PluginDescriptor descriptor,
            IEnumerable<string>? grantedPermissions);
    }

    public sealed class PluginPermissionEvaluation
    {
        public PluginPermissionEvaluation(
            bool granted,
            IReadOnlyList<string> missingPermissions,
            IReadOnlyList<string> unknownPermissions)
        {
            Granted = granted;
            MissingPermissions = missingPermissions;
            UnknownPermissions = unknownPermissions;
        }

        public bool Granted { get; }

        public IReadOnlyList<string> MissingPermissions { get; }

        public IReadOnlyList<string> UnknownPermissions { get; }
    }

    public sealed class PluginPermissionService : IPluginPermissionService
    {
        public PluginPermissionEvaluation Evaluate(
            PluginDescriptor descriptor,
            IEnumerable<string>? grantedPermissions)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            // Built-in plugins run inside the trusted host assembly and are not
            // governed by the external manifest permission model.
            if (!descriptor.IsExternal || descriptor.Permissions.Count == 0)
            {
                return new PluginPermissionEvaluation(
                    true,
                    Array.Empty<string>(),
                    Array.Empty<string>());
            }

            var granted = new HashSet<string>(StringComparer.Ordinal);
            if (grantedPermissions != null)
            {
                foreach (var permission in grantedPermissions)
                {
                    if (!string.IsNullOrWhiteSpace(permission))
                    {
                        granted.Add(permission);
                    }
                }
            }

            var missing = new List<string>();
            var unknown = new List<string>();

            foreach (var permission in descriptor.Permissions)
            {
                if (!PluginPermissions.IsKnown(permission))
                {
                    unknown.Add(permission);
                    continue;
                }

                if (!granted.Contains(permission))
                {
                    missing.Add(permission);
                }
            }

            return new PluginPermissionEvaluation(
                missing.Count == 0 && unknown.Count == 0,
                missing,
                unknown);
        }
    }

    /// <summary>
    /// Per-execution permission gate exposed through <see cref="PluginExecutionContext"/>.
    /// Plugin-level checks happen before activation; this interceptor is the
    /// in-execution defense-in-depth hook for future per-action permission calls.
    /// </summary>
    public interface IPluginPermissionInterceptor
    {
        bool IsGranted(string permission);

        void Demand(string permission);
    }

    public sealed class PluginPermissionDeniedException : Exception
    {
        public PluginPermissionDeniedException(string permission)
            : base($"Plugin permission is not granted: {permission}")
        {
            Permission = permission;
        }

        public string Permission { get; }
    }

    public sealed class GrantedPluginPermissionInterceptor : IPluginPermissionInterceptor
    {
        private readonly HashSet<string> _granted;

        public GrantedPluginPermissionInterceptor(IEnumerable<string>? grantedPermissions)
        {
            _granted = new HashSet<string>(StringComparer.Ordinal);

            if (grantedPermissions == null)
            {
                return;
            }

            foreach (var permission in grantedPermissions)
            {
                if (PluginPermissions.IsKnown(permission))
                {
                    _granted.Add(permission);
                }
            }
        }

        public bool IsGranted(string permission)
        {
            return PluginPermissions.IsKnown(permission) && _granted.Contains(permission);
        }

        public void Demand(string permission)
        {
            if (!IsGranted(permission))
            {
                throw new PluginPermissionDeniedException(permission);
            }
        }
    }

    public sealed class AllowAllPluginPermissionInterceptor : IPluginPermissionInterceptor
    {
        public static readonly AllowAllPluginPermissionInterceptor Instance = new();

        public bool IsGranted(string permission)
        {
            return PluginPermissions.IsKnown(permission);
        }

        public void Demand(string permission)
        {
            if (!IsGranted(permission))
            {
                throw new PluginPermissionDeniedException(permission);
            }
        }
    }
}
