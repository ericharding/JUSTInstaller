using System.Text;
using System.IO;

namespace JUSTInstaller;

// install path template
// Windows shortcut paths
// symlink paths


// NOTE: if you want environment variables in your instal path do it yourself
// Entrypoint: Name of the main executable. .exe is automatically added on Windows if not specified
// InstallPathTemplate - local directory to install. i.e. ~/.apps/myApp{version}.  Requires {version} and will expand ~ to the current user's home directory
// CurrentVersionUri - absolute url to check the current version. Must return a version in the form of 
//    <major>[.minor][.revision] [hash]
public record class InstallerConfig(
    string EntryPoint,
    string InstallPathTemplate,
    Uri CurrentVersionUri,
    string UpdateLocationTemplate,
    IEnumerable<string> WindowsShortcutPaths,
    IEnumerable<string> SymlinksPaths);

public class Installer
{
    public Installer(InstallerConfig config)
    {
    }

    public Version? CurrentVersion { get { return null; } }
    // public bool UpdateAvailable
    public async Task<bool> CheckForUpdate()
    {
        return false;
    }

}

internal class Utils
{
    public static string ExpandUser(string path)
    {
        if (!Path.IsPathFullyQualified(path) && path.IndexOf('~') > 0)
        {
            return path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }
        return path;
    }
    public static string ExpandVersion(string path, Version version)
    {
        return path.Replace("{version}", version.ToString());
    }

    public static Version? parseVersion(string version)
    {
        if (Version.TryParse(version, out var v))
        {
            return v;
        }
        else if (UInt32.TryParse(version, out var iv))
        {
            return new Version((int)iv, 0);
        }
        return null;
    }

    public static bool IsWindows
    {
        get
        {
            return System.Environment.OSVersion.Platform == PlatformID.Win32NT;
        }
    }

}
