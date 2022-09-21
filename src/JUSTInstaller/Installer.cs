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
    string? PostInstall = null,
    IEnumerable<string>? WindowsShortcutPaths = null,
    IEnumerable<string>? SymlinksPaths = null
    );

public class Installer
{
    private InstallerConfig _config;

    public Installer(InstallerConfig config) {
        _config = config;
        UpdateCurrentVersion();
    }

    public bool IsInstalled => CurrentVersion != null;
    public Version? CurrentVersion { get; init; }

    public async Task<bool> CheckforUpdate() {
        var stream = await Utils.DownloadFileAsync(_config.CurrentVersionUri);
        var data = await stream.ReadToEndAsync();
        var newVersion = parseVersionFromString(data);
        if (newVersion == null) {
            log_error($"Could not parse version from '{data}'");
            return false;
        }
        if (CurrentVersion == null || newVersion > CurrentVersion) {
            return true;
        }
        return false;
    }


    #region private methods

    private void log_info(string msg) { }
    private void log_error(string msg) { }
    
    private void UpdateCurrentVersion() {
        // Check version.txt in install path root
        // Set CurrentVersion
        throw new NotImplementedException();
    }


    private Version? parseVersionFromString(string data) {
        // Grab text until the first whitespace character (\n is whitespace)
        var firstToken = String.Concat(data.TakeWhile(ch => !Char.IsWhiteSpace(ch)));
        return Utils.parseVersion(firstToken);
    }


    #endregion

}

internal static class Utils
{
    public static string ExpandUser(string path) {
        if (!Path.IsPathFullyQualified(path) && path.IndexOf('~') > 0) {
            return path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }
        return path;
    }

    public static string ExpandVersion(string path, Version version) {
        return path.Replace("{version}", version.ToString());
    }

    public static Version? parseVersion(string version) {
        if (Version.TryParse(version, out var v)) {
            return v;
        }
        else if (UInt32.TryParse(version, out var iv)) {
            return new Version((int)iv, 0);
        }
        return null;
    }

    public static bool IsWindows {
        get {
            return System.Environment.OSVersion.Platform == PlatformID.Win32NT;
        }
    }

    public static async Task<StreamReader> DownloadFileAsync(Uri uri) {
        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var response = await httpClient.SendAsync(request);
        return new StreamReader(await response.Content.ReadAsStreamAsync());
        // var body = reader.ReadToend();
    }

}
