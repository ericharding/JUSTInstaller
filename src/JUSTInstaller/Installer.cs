﻿using System.Text;
using System.IO;
using System.Diagnostics;
using System.IO.Compression;

namespace JUSTInstaller;

// install path template
// Windows shortcut paths
// symlink paths


public record class InstallerConfig(
    string EntryPoint,  // Application entrypoint without path
    string InstallBasePath, // Root path where different versions get installed. A version.txt will be created here.
    string InstallFolderTemplate, // Name of the installed app folder relative to InstallFolderbase. Must include {version}
    Uri CurrentVersionUri, // where to check for a new version
    string UpdateLocationTemplate, // Where to get the new zip
    Version? CurrentVersion = null, // If null we use the version from assemblyinfo
    uint KeepVersion = 1, // How many old version to keep (not implemented)
    string? SettingsPath = null, // Path to store settings. Defaults to InstallBasePath/settings/
    IEnumerable<string>? WindowsShortcutPaths = null,
    IEnumerable<string>? SymlinksPaths = null,
    TimeSpan HttpTimeout = default
    );

public class Installer {
    private InstallerConfig _config;

    public Installer(InstallerConfig config) {
        _config = config.ExpandUser().EnsureVersion().EnsureDefaults();
        ValidateConfig(_config)?.Throw();
    }

    public bool IsInstalled => CurrentVersion != null;
    public Version? CurrentVersion => _config.CurrentVersion;

    public Version? AvailableVersion { get; set; }

    public event Action<string>? OnError;
    public event Action<string>? OnInfo;

    public async Task<bool> CheckforUpdate() {
        var reader = new StreamReader(await Utils.DownloadFileAsync(_config.CurrentVersionUri, _config.HttpTimeout).ConfigureAwait(false));
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

    private static bool? _isfirstRunOnCurrentVersion = null;
    public bool IsfirstRunOnCurrentVersion {
        get {
            if (_isfirstRunOnCurrentVersion.HasValue) {
                return _isfirstRunOnCurrentVersion.Value;
            }

            // White lie - This only checks if this is the firs time this accessor is called while running this version of the app

            if (string.IsNullOrEmpty(_config.SettingsPath)) {
                log_error("SettingsPath is null or empty. It must be non-empty and the current user must have write permission to use IsfirstRunOnCurrentVersion");
            }
            string settingsPath = _config.SettingsPath!;
            if (!Directory.Exists(settingsPath)) {
                Directory.CreateDirectory(settingsPath);
            }
            var localVersionPath = Path.Combine(settingsPath, "current_version.txt");
            Version? readSavedLocalVersion() {
                try {
                    if (File.Exists(localVersionPath)) {
                        return parseVersionFromString(File.ReadAllText(localVersionPath));
                    }
                } catch (Exception e) {
                    log_error($"Exception reading current version. {e}");
                }
                return null;
            }
            void saveCurrentVersion() {
                try {
                    File.WriteAllText(localVersionPath, CurrentVersion?.ToString());
                } catch (Exception e) {
                    log_error($"Exception saving current version. {e}");

                }
            }

            var currentVersion = readSavedLocalVersion();
            _isfirstRunOnCurrentVersion = currentVersion != CurrentVersion;
            if (_isfirstRunOnCurrentVersion.Value == true) {
                saveCurrentVersion();
            }

            return _isfirstRunOnCurrentVersion.Value;
        }
    }

    // Returns the installed version (if successful) and the path to the new entrypoint
    public async Task<Installer.InstalledVersion?> InstallUpdate(bool run = false, string runArgs = "") {
        // Download zip to temp
        if (AvailableVersion == null) {
            throw new InvalidOperationException("There is no new version to upgrade to.");
        }

        string downloadUri = Utils.ExpandVersion(_config.UpdateLocationTemplate, AvailableVersion);
        log_info($"Downloading {downloadUri}");

        string destinationFolder = Utils.ExpandVersion(_config.InstallFolderTemplate, AvailableVersion);
        string destination = Path.Combine(_config.InstallBasePath, destinationFolder);

        string tempFileName;
        try {
            tempFileName = await Utils.DownloadToTempFile(new Uri(downloadUri), _config.HttpTimeout).ConfigureAwait(false);
        } catch(Exception e) {
            log_error($"Exception downloading {downloadUri}. {e}");
            return null;
        }
        log_info($"Unzipping {tempFileName} to {destination}. Unzipping");

        // Unzip on background thread
        await Task.Factory.StartNew(() => {
            RemoveIfExists(destination);
            ZipFile.ExtractToDirectory(tempFileName, destination);
        }).ConfigureAwait(false);

        var entryPoint = Path.Combine(destination, _config.EntryPoint);
        entryPoint = Utils.TryOsSpecificExpansion(entryPoint);

        CreateLinks(entryPoint);

        if (run) {
            log_info($"Running {entryPoint}");
            Process.Start(new ProcessStartInfo(entryPoint, runArgs));
        }

        return new InstalledVersion(
            Version: AvailableVersion,
            EntryPoint: entryPoint
         );
    }

    public async Task<InstalledVersion?> InstallUpdateIfAvailable(bool run = true) {
        if (AvailableVersion == null) {
            await CheckforUpdate().ConfigureAwait(false);
        }
        if (AvailableVersion != null && AvailableVersion > CurrentVersion) {
            return await InstallUpdate(run).ConfigureAwait(false);
        }
        return null;
    }

    #region private methods

    private void log_error(string msg) {
        OnError?.Invoke(msg);
    }
    private void log_info(object msg) {
        OnInfo?.Invoke(msg?.ToString() ?? "");
    }

    private void RemoveIfExists(string destination) {
        if (Directory.Exists(destination)) {
            var backupDirectory = $"{destination}_backup";
            log_info($"{destination} already exists. Moving to {backupDirectory}");
            if (Directory.Exists(backupDirectory)) {
                log_info($"{backupDirectory}, also already exists. Removing.");
                Directory.Delete(backupDirectory, true);
            }
            Directory.Move(destination, backupDirectory);
        }
    }

    private void CreateLinks(string entryPoint) {
        if (Utils.IsWindows) {
            foreach (var link in _config.WindowsShortcutPaths ?? Enumerable.Empty<string>()) {
                TryCreateWindowsShortcut(link, entryPoint);
            }
        }

        foreach (var link in _config.SymlinksPaths ?? Enumerable.Empty<string>()) {
            TryCreateSymLink(link, entryPoint);
        }
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

    private bool TryCreateSymLink(string link, string dest) {
        try {
            var linkPath = link.ExpandUser();
            if (File.Exists(linkPath)) {
                log_info($"Symlink {linkPath} already exists. Overwriting.");
                File.Delete(linkPath);
            }
            Directory.CreateDirectory(new FileInfo(linkPath).DirectoryName ?? "");
            Directory.CreateSymbolicLink(link.ExpandUser(), dest);
            log_info($"Created symlink {link} to {dest}");
            return true;
        } catch (Exception e) {
            log_error($"Failed to create symlink {link}. {e}");
            return false;
        }
    }

    private object TryCreateWindowsShortcut(string link, string dest) {
        try {
            var shortcut = new WindowsShortcutFactory.WindowsShortcut() {
                Path = dest
            };
            shortcut.Save(Utils.AddExtensionIfNotPresent(link.ExpandUser(), ".lnk"));
            log_info($"Created shortcut {link} to {dest}");
            return true;
        } catch (Exception e) {
            log_error($"Failed to create shortcut {link}. {e}");
            return false;
        }
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

    public static InstallerConfig EnsureDefaults(this InstallerConfig config) {
        if (string.IsNullOrEmpty(config.SettingsPath)) {
            string settingsPath = Path.Join(config.InstallBasePath, "settings");
            return config with { SettingsPath = settingsPath };
        }
        return config;
    }
}

internal static class Utils {
    public static string ExpandUser(this string path) {
        if (!Path.IsPathFullyQualified(path) && path.IndexOf('~') >= 0) {
            return path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }
        return path;
    }

    public static string ExpandVersion(this string path, Version version) {
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

    public static async Task<Stream> DownloadFileAsync(Uri uri, TimeSpan timeout) {
        using var httpClient = new HttpClient();
        if (timeout != default) {
            httpClient.Timeout = timeout;
        }
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    }

    public static async Task<string> DownloadToTempFile(Uri downloadUri, TimeSpan timeout) {
        using var stream = await Utils.DownloadFileAsync(downloadUri, timeout).ConfigureAwait(false);
        var tempFileName = Path.GetTempFileName();
        using var tempFile = File.OpenWrite(tempFileName);
        await stream.CopyToAsync(tempFile).ConfigureAwait(false);
        return tempFileName;
    }

    internal static string TryOsSpecificExpansion(string entryPoint) {
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
            }
            else {
                return $"{path}.{extension}";
            }
        }
        return path;
    }

    public static void Throw(this Exception e) {
        throw e;
    }

}
