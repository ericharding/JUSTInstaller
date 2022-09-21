
# J.ust U.nzip S.ome T.hings

Cross platform installer that values sipmlicity.


# Options

* EntryPoint  
    The name of the executable or script that starts the application. Should **not** include the install path. If the name you give does not exist after unzipping the package '.exe' is appended.

* PostInstall  
    The name of an executable to run directly after install. Like EntryPoint, `.exe` will be appended as necessary.


* InstallPathTemplate  
    Where on the filesystem you want the application to be installed. For example `~/.apps/myApp_{version}/` or `c:/program files/MyApp/MyApp_v{version}`

* CurrentVersionUri  
    This is how we check for updates.  Send a GET request to the given URI and get back the result text.  (This is probably just a file on your webserver.) Assume the first line of the result is a version number and parse it. Compare against the current version.  If the downloaded file is larger get that version from the UpdateLocationTemplate.  
    **For example**: If we are currently on version 1.0 and CurrentVersionUri is `http://mydomain.com/myapp/version.txt` and the first line of version.txt is "1.1" we need to update so use "1.1" as the {version} in the UpdateLocationTemplate and download that to the InstallPathTemplate and update any links.


* UpdateLocationTemplate  
    Where to get the new files when an update is needed.
    Example: http://mydomain.com/myapp/downloads/{{version}}.zip


* WindowsShortcutPaths
* SymlinksPaths

# Recipes / FAQ

## I want to install on multiple platforms!

Sure! When you name your archives append a platform specific suffix. Then configure your installer with the same version URI and detect the current platform when setting your download URI.
```csharp
var platform = System.Environment.OSVersion.Platform switch {
            PlatformID.Unix => "nix",
            PlatformID.Win32NT => "win",
        };
config.UpdateLocationTemplate = $"https://example.com/downloads/myapp_{{version}}_{platform}";
```

## Hey! Why doesn't this installer force you to install into the standard, recommended, install locations? 

It does, man. You just have to tell it where that is.

## I changed my shortcuts and I need this version to remove the old shortcut location

No built in support but you could use `PostInstall`.

## Can PostInstall be a script?

Sure. Just be careful if you're installing on multiple platforms. 

## I'd like to be able to uninstall my app using my favorite system control panel applet.

I'd like a pony.

