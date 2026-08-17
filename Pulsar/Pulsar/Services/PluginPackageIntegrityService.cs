using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;

namespace Pulsar.Services
{
    /// <summary>
    /// SHA-256 based tamper-evident record plus optional RSA signature
    /// verification for external plugin packages.
    ///
    /// Unsigned packages remain installable in "developer mode" (no trusted
    /// publisher keys configured). Once at least one trusted public key exists,
    /// an unsigned package fails closed.
    /// </summary>
    public sealed class PluginPackageIntegrityService : IPluginPackageIntegrityVerifier
    {
        private const string IntegrityFileName = "integrity.json";
        private const string SignatureSuffix = ".pulsar.sig";
        private const string SchemaVersion = "1";

        private readonly string _trustedPublishersDirectory;
        private readonly ILogger<PluginPackageIntegrityService> _logger;

        public PluginPackageIntegrityService(
            ILogger<PluginPackageIntegrityService> logger,
            string? trustedPublishersDirectory = null)
        {
            _logger = logger;

            _trustedPublishersDirectory = trustedPublishersDirectory
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Pulsar",
                    "TrustedPublishers");

            Directory.CreateDirectory(_trustedPublishersDirectory);
        }

        public async Task<PluginPackageIntegrityResult> VerifyArchiveAsync(
            string archivePath,
            CancellationToken cancellationToken = default)
        {
            var archiveBytes = await File.ReadAllBytesAsync(archivePath, cancellationToken);
            var archiveSha256 = Convert.ToHexString(SHA256.HashData(archiveBytes)).ToLowerInvariant();

            var signaturePath = archivePath + SignatureSuffix;
            if (!File.Exists(signaturePath))
            {
                var trustedKeys = LoadTrustedPublishers();
                if (trustedKeys.Count > 0)
                {
                    return PluginPackageIntegrityResult.Failed(
                        $"Package is not signed but {trustedKeys.Count} trusted publisher key(s) are configured.",
                        archiveSha256);
                }

                _logger.LogWarning(
                    "[PluginPackageIntegrity] No trusted publisher keys configured; accepting unsigned package {Archive} (developer mode).",
                    Path.GetFileName(archivePath));

                return new PluginPackageIntegrityResult(
                    PluginPackageIntegrityStatus.Unsigned,
                    archiveSha256);
            }

            try
            {
                var signature = JsonSerializer.Deserialize<PluginPackageSignatureEnvelope>(
                    await File.ReadAllTextAsync(signaturePath, cancellationToken),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (signature == null
                    || string.IsNullOrWhiteSpace(signature.PublicKeyThumbprint)
                    || string.IsNullOrWhiteSpace(signature.Signature))
                {
                    return PluginPackageIntegrityResult.Failed(
                        "Invalid package signature envelope.",
                        archiveSha256);
                }

                var trustedKeys = LoadTrustedPublishers();
                var publicKey = trustedKeys.FirstOrDefault(k =>
                    string.Equals(k.Thumbprint, signature.PublicKeyThumbprint, StringComparison.OrdinalIgnoreCase));

                if (publicKey == null)
                {
                    return PluginPackageIntegrityResult.Failed(
                        $"Signature public key thumbprint '{signature.PublicKeyThumbprint}' is not trusted.",
                        archiveSha256);
                }

                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(publicKey.Key, out _);

                var signatureBytes = Convert.FromBase64String(signature.Signature);
                var valid = rsa.VerifyData(
                    archiveBytes,
                    signatureBytes,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                if (!valid)
                {
                    return PluginPackageIntegrityResult.Failed(
                        "Package signature verification failed.",
                        archiveSha256);
                }

                _logger.LogInformation(
                    "[PluginPackageIntegrity] Signature verified for {Archive}; publisher={Publisher}",
                    Path.GetFileName(archivePath),
                    signature.Publisher);

                return new PluginPackageIntegrityResult(
                    PluginPackageIntegrityStatus.SignatureVerified,
                    archiveSha256,
                    publisher: signature.Publisher);
            }
            catch (Exception ex) when (ex is IOException or JsonException or FormatException or CryptographicException)
            {
                _logger.LogWarning(ex, "[PluginPackageIntegrity] Signature verification failed for {Archive}", archivePath);
                return PluginPackageIntegrityResult.Failed(
                    $"Package signature verification failed: {ex.Message}",
                    archiveSha256);
            }
        }

        public Task<PluginPackageIntegrityResult> VerifyExtractedAsync(
            string extractPath,
            IReadOnlyDictionary<string, string> expectedFileHashes,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedFileHashes);

            if (expectedFileHashes.Count == 0)
            {
                return Task.FromResult(new PluginPackageIntegrityResult(
                    PluginPackageIntegrityStatus.VerifiedHash,
                    null));
            }

            var actual = ComputeFileHashes(extractPath);

            foreach (var expected in expectedFileHashes)
            {
                var normalizedPath = NormalizeRelativePath(expected.Key);
                if (!actual.TryGetValue(normalizedPath, out var actualHash))
                {
                    return Task.FromResult(PluginPackageIntegrityResult.Failed(
                        $"Manifest hash references missing file: {expected.Key}"));
                }

                if (!string.Equals(actualHash, expected.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(PluginPackageIntegrityResult.Failed(
                        $"File hash mismatch: {expected.Key}"));
                }
            }

            return Task.FromResult(new PluginPackageIntegrityResult(
                PluginPackageIntegrityStatus.VerifiedHash,
                null));
        }

        public async Task<PluginPackageIntegrityResult> WriteInstallRecordAsync(
            string installPath,
            string? archiveSha256,
            CancellationToken cancellationToken = default)
        {
            var files = ComputeFileHashes(installPath);

            var record = new PluginPackageInstallRecord
            {
                SchemaVersion = SchemaVersion,
                PackageSha256 = archiveSha256,
                Files = files
            };

            var json = JsonSerializer.Serialize(
                record,
                new JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(
                Path.Combine(installPath, IntegrityFileName),
                json,
                cancellationToken);

            return new PluginPackageIntegrityResult(
                PluginPackageIntegrityStatus.VerifiedHash,
                archiveSha256);
        }

        public Task<PluginPackageIntegrityResult> VerifyInstalledAsync(
            string installPath,
            CancellationToken cancellationToken = default)
        {
            var integrityPath = Path.Combine(installPath, IntegrityFileName);
            if (!File.Exists(integrityPath))
            {
                return Task.FromResult(PluginPackageIntegrityResult.Failed(
                    "Installed plugin has no integrity record."));
            }

            try
            {
                var record = JsonSerializer.Deserialize<PluginPackageInstallRecord>(
                    File.ReadAllText(integrityPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (record == null || record.Files == null)
                {
                    return Task.FromResult(PluginPackageIntegrityResult.Failed(
                        "Installed plugin integrity record is invalid."));
                }

                var actual = ComputeFileHashes(installPath);
                if (record.Files.Count != actual.Count)
                {
                    return Task.FromResult(PluginPackageIntegrityResult.Failed(
                        "Installed plugin file set changed after installation."));
                }

                foreach (var expected in record.Files)
                {
                    if (!actual.TryGetValue(expected.Key, out var actualHash)
                        || !string.Equals(actualHash, expected.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.FromResult(PluginPackageIntegrityResult.Failed(
                            $"Installed plugin file changed after installation: {expected.Key}"));
                    }
                }

                return Task.FromResult(new PluginPackageIntegrityResult(
                    PluginPackageIntegrityStatus.VerifiedHash,
                    record.PackageSha256));
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                return Task.FromResult(PluginPackageIntegrityResult.Failed(
                    $"Failed to verify installed plugin: {ex.Message}"));
            }
        }

        private Dictionary<string, string> ComputeFileHashes(string root)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                var relativePath = NormalizeRelativePath(Path.GetRelativePath(root, file));
                if (string.Equals(Path.GetFileName(file), IntegrityFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result[relativePath] = Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
            }

            return result;
        }

        private List<TrustedPublisherKey> LoadTrustedPublishers()
        {
            var result = new List<TrustedPublisherKey>();

            foreach (var file in Directory.GetFiles(_trustedPublishersDirectory, "*.pem"))
            {
                try
                {
                    var pem = File.ReadAllText(file);
                    var keyBytes = ParsePublicKeyPem(pem);
                    result.Add(new TrustedPublisherKey(
                        Convert.ToHexString(SHA256.HashData(keyBytes)).ToLowerInvariant(),
                        keyBytes,
                        Path.GetFileNameWithoutExtension(file)));
                }
                catch (Exception ex) when (ex is IOException or CryptographicException or FormatException)
                {
                    _logger.LogWarning(ex, "[PluginPackageIntegrity] Failed to load trusted publisher key {Path}", file);
                }
            }

            return result;
        }

        private static byte[] ParsePublicKeyPem(string pem)
        {
            const string begin = "-----BEGIN PUBLIC KEY-----";
            const string end = "-----END PUBLIC KEY-----";

            var start = pem.IndexOf(begin, StringComparison.Ordinal);
            var finish = pem.IndexOf(end, StringComparison.Ordinal);
            if (start < 0 || finish <= start)
            {
                throw new FormatException("Public key PEM markers are missing.");
            }

            var body = pem.Substring(start + begin.Length, finish - start - begin.Length);
            return Convert.FromBase64String(body);
        }

        private static string NormalizeRelativePath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .TrimStart('/');
        }

        private sealed class PluginPackageInstallRecord
        {
            public string SchemaVersion { get; set; } = "1";

            public string? PackageSha256 { get; set; }

            public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class PluginPackageSignatureEnvelope
        {
            public string Publisher { get; set; } = string.Empty;

            public string PublicKeyThumbprint { get; set; } = string.Empty;

            public string Algorithm { get; set; } = "RSA-SHA256";

            public string Signature { get; set; } = string.Empty;
        }

        private sealed class TrustedPublisherKey
        {
            public TrustedPublisherKey(string thumbprint, byte[] key, string name)
            {
                Thumbprint = thumbprint;
                Key = key;
                Name = name;
            }

            public string Thumbprint { get; }

            public byte[] Key { get; }

            public string Name { get; }
        }
    }
}
