# Debugging from IDE

The source could should contain settings that are ready to run from Visual Studio or VS Code

## Visual Sudio
 - Startup Project: Set the `UI\iPath.Blazor.Server` project to your start up project
 - Launch Settings: `Properties\launchSettings.json` contains some basics
    	- A configuration path can be provided over an env variable `CONFIG_PATH`
    	- there is an example `"CONFIG_PATH": "C:/Daten/ipath_sqlite"` but as long as this folder does not exist, 
    	  the setting is ignored
 - App Settings: Basics are provided in `appsettings.json`. Costomization can be achived by
      - Add User Secrets
      - create a folder for iPath data and place a copy of `appsettings.json` there and point the
        `CONFIG_PATH`  env to this folder
     
## Initial launch
On startup a database will be create and an initial Admin user is created. The initial password is visible on 
first login.

## Application data
By default (appsetting.json) all data will be stored in ./ipath_data folder. This will be created relative to 
the startup project. So by default as `src\ui\iPath.Blazor.Server\ipath_data`. This contains logs, database, 
uploaded pictures.
