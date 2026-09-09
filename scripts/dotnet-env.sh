# dotnet-env.sh - Windows environment prelude for POSIX shells (git-bash / MSYS / WSL-ish)
#
# WHY THIS EXISTS
# ---------------
# Some AI/agent hosts spawn their shell WITHOUT the standard Windows environment
# variables (APPDATA, LOCALAPPDATA, ProgramData, ProgramFiles, ProgramFiles(x86),
# CommonProgramFiles, SystemRoot, windir, ...). .NET 8's NuGet stack resolves the
# machine-wide settings directory through Environment.GetFolderPath, and when the
# underlying variable is missing it throws:
#
#     NuGet.targets(745,5): error : The type initializer for
#     'NuGet.Configuration.ConfigurationDefaults' threw an exception.
#       ---> System.ArgumentNullException: Value cannot be null. (Parameter 'path1')
#         at System.IO.Path.Combine(String path1, String path2)
#         at NuGet.Common.NuGetEnvironment.GetFolderPath(NuGetFolderPath folder)
#         at NuGet.Configuration.XPlatMachineWideSetting..ctor()
#         at NuGet.Build.Tasks.GetRestoreSettingsTask.Execute()
#
# Symptom: EVERY project in the solution fails at restore, even projects you never
# touched - so it looks like your code broke the build. It did not.
#
# scripts/dev.ps1 already performs the same repair for PowerShell hosts. This file
# is the POSIX-shell twin: `source` it before any dotnet command.
#
# USAGE
#   source scripts/dotnet-env.sh        # from the repo root
#   dotnet build Pulsar/Pulsar.sln
#   dotnet test  Pulsar/Pulsar.Tests/Pulsar.Tests.csproj
#
# Only MISSING variables are patched; existing values are never overwritten.
# ASCII-only on purpose (safe to source from MSYS with any locale).

_pulsar_set_env() {
    # $1 = name, $2 = fallback
    if [ -z "$(printenv "$1" 2>/dev/null)" ]; then
        export "$1=$2"
    fi
}

_pulsar_user_profile="${USERPROFILE:-}"
if [ -z "$_pulsar_user_profile" ]; then
    if [ -n "$HOMEDRIVE" ] && [ -n "$HOMEPATH" ]; then
        _pulsar_user_profile="${HOMEDRIVE}${HOMEPATH}"
    elif [ -n "$USERNAME" ]; then
        _pulsar_user_profile="C:\\Users\\${USERNAME}"
    else
        _pulsar_user_profile="C:\\Users\\$(whoami 2>/dev/null || echo unknown)"
    fi
fi

_pulsar_system_drive="${SystemDrive:-C:}"

_pulsar_set_env USERPROFILE  "$_pulsar_user_profile"
_pulsar_set_env APPDATA      "${_pulsar_user_profile}\\AppData\\Roaming"
_pulsar_set_env LOCALAPPDATA "${_pulsar_user_profile}\\AppData\\Local"
_pulsar_set_env ProgramData  "${_pulsar_system_drive}\\ProgramData"
_pulsar_set_env ALLUSERSPROFILE "${_pulsar_system_drive}\\ProgramData"
_pulsar_set_env PUBLIC       "${_pulsar_system_drive}\\Users\\Public"
_pulsar_set_env SystemRoot   "${_pulsar_system_drive}\\Windows"
_pulsar_set_env windir       "${_pulsar_system_drive}\\Windows"
_pulsar_set_env SystemDrive  "$_pulsar_system_drive"
_pulsar_set_env ComSpec      "${_pulsar_system_drive}\\Windows\\System32\\cmd.exe"
_pulsar_set_env PATHEXT      ".COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC"
_pulsar_set_env OS           "Windows_NT"

# ProgramFiles / ProgramFiles(x86): NuGet's machine-wide settings root is derived
# from these; missing ProgramFiles was the actual 'path1' null in the 2026-09-09
# investigation (bare APPDATA+ProgramData alone did NOT fix it).
if [ -z "$(printenv 'ProgramFiles' 2>/dev/null)" ]; then
    if [ -n "$(printenv 'ProgramW6432' 2>/dev/null)" ]; then
        _pulsar_set_env ProgramFiles "$(printenv 'ProgramW6432')"
    else
        _pulsar_set_env ProgramFiles "${_pulsar_system_drive}\\Program Files"
    fi
fi

# NOTE: ProgramFiles(x86) / CommonProgramFiles(x86) contain parentheses, which a
# POSIX shell cannot export ("not a valid identifier"). Empirically the
# 2026-09-09 failure only needed ProgramFiles. If a future host needs the x86
# variants, run through scripts/dev.ps1 (it CAN set them) or wrap the command:
#   env "ProgramFiles(x86)=C:\Program Files (x86)" dotnet build ...

if [ -z "$(printenv 'ProgramW6432' 2>/dev/null)" ]; then
    _pulsar_set_env ProgramW6432 "${_pulsar_system_drive}\\Program Files"
fi

_pulsar_set_env CommonProgramFiles "$(printenv 'ProgramFiles')\\Common Files"
_pulsar_set_env CommonProgramW6432 "$(printenv 'ProgramFiles')\\Common Files"

unset _pulsar_set_env _pulsar_user_profile _pulsar_system_drive _pulsar_pf _pulsar_pf86
