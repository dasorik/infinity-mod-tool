# :wrench: Infinity Mod Tool

Infinity Mod Tool is a mod loading program that allows for the installation/uninstallation of various mods within Disney Infinity 3.0

_NOTE! - This tool is under active development, so old versions of mods may not work with newer versions of this tool. If you run into problems with installing/uninstalling mods, try deleting /Data/UserSettings.json_

## Upcoming Features

- Support for playsets
- Support for other types of mods

## Creating Character Mods

No actual mod files have been included in this repository, but can easily be created by adding a .zip file under the `/Mods` folder containing the following files

- `config.json` (see format below) 
- `presentation.json` (Character specific - this will be the data written to virtualreaderpc_data.lua if required)
- `portrait.png` (Can be any image in any format, configure name/format in config.json)

The `config.json` file should have the following 

```
{
  "Version": 1.0,
  "ModID": "Dasorik.ExampleCharacter", // Make this unique
  "ModCategory": "Character", // Available types ['Character']
  "ReplaceCharacter": false, // (Character specific) Set to true if this character mod needs to replace a character in the 'locks__lua.chd' file
  "WriteToCharacterList": true, // (Character specific) Set to true if this character's json data needs to be written to 'virtualreaderpc_data.lua'
  "DisplayName": "Example Character", // This is the name as shown on the installation button
  "DisplayColor": "purple", // This affects the frames of the character portrait, ie. "red"/"purple"
  "DisplayImage": "portrait.png" // This will be displayed as the portrait image on the installation button, relative to root mod path
}
```

## Building

This tool runs in Blazor via [ElectronNet](https://github.com/ElectronNET/Electro.NET). To get this project running, please follow the steps provided in the linked ElectronNet repository.

## Additional Credits
This mod tool uses external tools, with attributions below:

[QuickBMS](https://aluigi.altervista.org/quickbms.htm) - Released under [GPL-2.0](http://www.gnu.org/licenses/old-licenses/gpl-2.0.txt)

[Unluac (.Net port)](https://github.com/HansWessels/unluac) - Released under the following [license](https://github.com/dasorik/infinity-mod-tool/blob/master/InfinityModTool/Lib/UnluacNet/UnluacNet-LICENSE.txt)
