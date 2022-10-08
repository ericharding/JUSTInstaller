using System.Text;
using System.IO;
using System.Diagnostics;
using System.IO.Compression;

namespace JUSTInstaller;

// install path template
// Windows shortcut paths
// symlink paths


public record class InstallerConfig(
    string EntryPoint,  // Application entrypoint
    string InstallBasePath, // Root path where different versions get installed. A version.txt will be created here.
    string InstallFolderTemplate, // Name of the installed app folder relative to InstallFolderbase. Must include {version}
    Uri CurrentVersionUri, // where to check for a new version
    string UpdateLocationTemplate, // Where to get the new zip
    Version? CurrentVersion = null, // If null we use the version from assemblyinfo
    uint KeepVersion = 2, // How many old version to keep
    IEnumerable<string>? WindowsShortcutPaths = null,
    IEnumerable<string>? SymlinksPaths = null
    );

public class Installer
{
    private InstallerConfig _config;

    public Installer(InstallerConfig config) {
        _config = config.ExpandUser().EnsureVersion();
        ValidateConfig(_config)?.Throw();
    }

    public bool IsInstalled => CurrentVersion != null;
    public Version? CurrentVersion => _config.CurrentVersion;

    public Version? AvailableVersion { get; set; }

    public event Action<string>? OnError;
    public event Action<string>? OnInfo;

    public async Task<bool> CheckforUpdate() {
        var reader = new StreamReader(await Utils.DownloadFileAsync(_config.CurrentVersionUri));
        var data = await reader.ReadToEndAsync();
        var newVersion = parseVersionFromString(data);
        AvailableVersion = newVersion;
        if (newVersion == null) {
            log_error($"Could not parse version from '{data}'");
            return false;
        }
        if (CurrentVersion == null || newVersion > CurrentVersion) {
            return true;
        }
        return false;
    }

    public record class InstalledVersion(Version Version, string EntryPoint);

    // Returns the installed version (if successful) and the path to the new entrypoint
    public async Task<Installer.InstalledVersion?> InstallUpdate(bool run=false, string runArgs="") {
        // Download zip to temp
        if (AvailableVersion == null) {
            throw new InvalidOperationException("There is no new version to upgrade to.");
        }

        string downloadUri = Utils.ExpandVersion(_config.UpdateLocationTemplate, AvailableVersion);
        log_info($"Downloading {downloadUri}");

        string destinationFolder = Utils.ExpandVersion(_config.InstallFolderTemplate, AvailableVersion);
        string destination = Path.Combine(_config.InstallBasePath, destinationFolder);

        var tempFileName = await Utils.DownloadToTempFile(new Uri(downloadUri));
        log_info($"Unzipping {tempFileName} to {destination}. Unzipping");

        // Unzip on background thread
        await Task.Factory.StartNew(() => ZipFile.ExtractToDirectory(tempFileName, destination));

        var entryPoint = Path.Combine(destination, _config.EntryPoint);
        entryPoint = Utils.TryOsSpecificExpansion(entryPoint);
        if (run) {
            log_info($"Running {entryPoint}");
            Process.Start(new ProcessStartInfo(entryPoint, runArgs));
        }

        // Create/Update links
        CreateLinks(entryPoint);

        return new InstalledVersion( 
            Version: AvailableVersion,
            EntryPoint: entryPoint
         );
    }

    private void CreateLinks(string entryPoint)
    {
        foreach (var link in _config.WindowsShortcutPaths ?? Enumerable.Empty<string>()) {
            var shortcut = new WindowsShortcutFactory.WindowsShortcut() {
                Path = entryPoint
            };
            shortcut.Save(Utils.AddExtensionIfNotPresent(link, ".lnk"));
        }

        foreach (var link in _config.SymlinksPaths ?? Enumerable.Empty<string>()) {
            Directory.CreateSymbolicLink(link, entryPoint);
        }
    }

    public async Task<Version?> InstallUpdateIfAvailable() {
        if (AvailableVersion == null) {
            await CheckforUpdate();
        }
        if (AvailableVersion != null && AvailableVersion > CurrentVersion) {
            await InstallUpdate();
        }
        return null;
    }

    #region private methods

    private void log_error(string msg)  {
        OnError?.Invoke(msg);
    }
    private void log_info(object msg) {
        OnInfo?.Invoke(msg?.ToString() ?? "");
    }

    internal static Exception? ValidateConfig(InstallerConfig config) {
        if (!Path.IsPathFullyQualified(Utils.ExpandUser(config.InstallBasePath))) {
            return new ArgumentException("InstallBasePath must be an absolute path");
        }
        if (!config.InstallFolderTemplate.Contains("{version}")) {
            return new ArgumentException("InstalleFolderTemplate must contain the version replacement token {version}");
        }
        return null;
    }
    
    private Version? parseVersionFromString(string data) {
        // Grab text until the first whitespace character (\n is whitespace)
        var firstToken = String.Concat(data.TakeWhile(ch => !Char.IsWhiteSpace(ch)));
        return Utils.parseVersion(firstToken);
    }


    #endregion

}

internal static class InstallerConfigUtils {
    public static InstallerConfig ExpandUser(this InstallerConfig config) {
        if (config.InstallBasePath.Contains("~")) {
            return config with { InstallBasePath = Utils.ExpandUser(config.InstallBasePath) };
        }
        return config;
    }

    public static InstallerConfig EnsureVersion(this InstallerConfig config) {
        if (config.CurrentVersion == null) {
            var currentVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            return config with { CurrentVersion = currentVersion };
        }
        return config;
    }
}

internal static class Utils
{
    public static string ExpandUser(string path) {
        if (!Path.IsPathFullyQualified(path) && path.IndexOf('~') >= 0) {
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

    public static async Task<Stream> DownloadFileAsync(Uri uri) {
        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var response = await httpClient.SendAsync(request);
        return await response.Content.ReadAsStreamAsync();
    }

    public static async Task<string> DownloadToTempFile(Uri downloadUri) {
        using var stream = await Utils.DownloadFileAsync(downloadUri);
        var tempFileName = Path.GetTempFileName();
        using var tempFile = File.OpenWrite(tempFileName);
        await stream.CopyToAsync(tempFile);
        return tempFileName;
    }

    internal static string TryOsSpecificExpansion(string entryPoint)
    {
        if (IsWindows) {
            string entrypointWithExtension = AddExtensionIfNotPresent(entryPoint, ".exe");
            if (File.Exists(entrypointWithExtension)) {
                return entrypointWithExtension;
            }
        }
        return entryPoint;
    }

    public static string AddExtensionIfNotPresent(string path, string extension) {
        if (!path.EndsWith(extension)) {
            if (extension.StartsWith(".")) {
                return $"{path}{extension}";
            } else {
                return $"{path}.{extension}";
            }
        }
        return path;
    }
}

internal static class ExtensionMethods {
    public static void Throw(this Exception e) {
        throw e;
    }
}
