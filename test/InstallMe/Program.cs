using JUSTInstaller;
using static System.Environment;

await main();

static async Task main()
{
    Installer installer = new Installer(new InstallerConfig(
        EntryPoint:"InstallMe", 
        InstallBasePath:"~/apps/install_me", 
        InstallFolderTemplate:"version_{version}",
        CurrentVersionUri:new Uri("http://digitalsorcery.net/InstallMe/version.txt"), 
        UpdateLocationTemplate:"http://digitalsorcery.net/InstallMe/download/InstallMe_{version}.zip",
        // WindowsShortcutPaths are automatically ignored if not installing to Windows
        WindowsShortcutPaths: new [] {
            Path.Combine(Environment.GetFolderPath(SpecialFolder.Desktop), "InstallMe")
        },
        // SymlinkPaths are created on all platforms
        SymlinksPaths: new[] { "~/bin/installme" }
    ));

    installer.OnError += Console.WriteLine;
    installer.OnInfo += Console.WriteLine;

    Console.WriteLine($"Current version {installer.CurrentVersion}");
    Console.Write($"Update available? ");
    bool updateAvailable = await installer.CheckforUpdate();
    if (updateAvailable) {
        Console.WriteLine($"Yes - {installer.AvailableVersion}");
    } else { Console.WriteLine("No"); }

    var resp = Prompt("Update now?", "y", "n");
    if (resp == "y") {
        await installer.InstallUpdate(run:false);
    }

    bool running = true;
    while(running) {
        running = Prompt("Quit?", "y", "n") == "n";
    }
}

static string Prompt(string prompt, params string[] options) {
    Console.WriteLine($"{prompt} {string.Join('/', options)}");
    string? ans = null;
    while(!options.Contains(ans)) {
        try {
            ans = Console.ReadLine();
        } catch (IOException) {
            break;
        }
    }
    return ans!;
}


