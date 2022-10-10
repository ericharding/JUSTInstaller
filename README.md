
# J.ust U.nzip S.ome T.hings

Cross platform installer that values sipmlicity.


# Options

* EntryPoint  
    The name of the executable or script that starts the application. Should **not** include the install path. On Windows '.exe' will be appended if necessary.

* InstallBasePath / InstallFolderTemplate
    Where on the filesystem you want the application to be installed. 
    For example { InstallBasePath=`~/.apps/myapp/`, InstallFolderTemplate=`myApp_{version}`} 
                { InstallBasePath=`c:\program files\MyApp`, InstallFolderTemplate=`/MyApp_v{version}` }

* CurrentVersionUri  
    This is how we check for updates.  Send a GET request to the given URI and get back the result text.  (This is probably just a file on your webserver.) Assume the first line of the result is a version number and parse it. Compare against the current version.  If the downloaded file is larger get that version from the UpdateLocationTemplate.  
    **For example**: If we are currently on version 1.0 and CurrentVersionUri is `http://mydomain.com/myapp/version.txt` and the first line of version.txt is "1.1" we need to update so use "1.1" as the {version} in the UpdateLocationTemplate and download that to the InstallPathTemplate and update any links.

* UpdateLocationTemplate  
    Where to get the new files when an update is needed.
    Example: http://mydomain.com/myapp/downloads/{{version}}.zip

* CurrentVersion  
    If you don't want to use the version from the project file you can override it here.

* KeepVersion (not implemented)
    Keep this many previous versions to allow quick roll back.

* WindowsShortcutPaths
    Shortcuts (.lnk) to the application entrypoint. These will be updated when a new version is installed.  If the installer is running on a non-Windows platform these are ignored.

* SymlinksPaths
    Symlinks to the application entrypoint. These will be updated when a new version is installed.

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

## How do I set the current version for my application?

Set ```<Version>1.2.3</Version>``` in your csproj or supply it as a build parameter with ```/p:Version=1.2.3``` 

## What if the version of my application doesn't match the version from my {CurrentVersionUri}?

If your version is lower than {CurrentVersionUri} the application will *always* try to update. If your version is equal or higher it will never update.

## Hey! Why doesn't this installer install into the standard, recommended, install locations for the given platform? 

It can! You just have to tell it where that is.

## I changed my shortcuts and I need this version to remove the old shortcut location

No built in support but you can check IsfirstRunOnCurrentVersion and do some custom cleanup.

## I'd like my application to install into a priveledged location like c:/program files/

First, it's probably easier to just install per-user. However, if you want to you can accomplish this one of two ways.  

1. Using application manifests which require elevated priveledges. ```<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />```.  As a warning, this functionality isn't uniformly supported cross platform.  

2. When Installer.CheckForUpdate() returns true spawn a separate process under platform specific elelvation. Have that process call the installer to do the update.

## Does JUSTinstaller support uninstall?

Not currently.

## How does IsfirstRunOnCurrentVersion work?

It doesn't. (TODO)

# Design Notes

* The installed application is an unziped zip file into {InstallBaesPath}/{InstallFolderTemplate}.
