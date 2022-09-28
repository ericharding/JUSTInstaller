using JUSTInstaller;


await main();

static async Task main()
{
    Installer installer = new Installer(new InstallerConfig(
        EntryPoint:"InstallMe", 
        InstallBasePath:"~/apps/install_me", 
        InstallFolderTemplate:"version_{version}",
        CurrentVersionUri:new Uri("http://digitalsorcery.net/InstallMe/version.txt"), 
        UpdateLocationTemplate:"http://digitalsorcery.net/InstallMe/download/InstallMe_{version}.zip"));

    Console.WriteLine($"Current version {installer.CurrentVersion}");
    Console.Write($"Update available? ");
    bool updateAvailable = await installer.CheckforUpdate();
    if (updateAvailable) {
        Console.WriteLine($"Yes - {installer.AvailableVersion}");
    } else { Console.WriteLine("No"); }

    Console.WriteLine("Update now?");
    var resp = Prompt("Update now?", "y", "n");
    if (resp == "y") {
        // installer.ins
    }


}

static string Prompt(string prompt, params string[] options) {
    Console.WriteLine($"{prompt} {string.Join('/', options)}");
    string? ans = null;
    while(!options.Contains(ans)) {
        ans = Console.ReadLine();
    }
    return ans;
}


