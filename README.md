# :wrench: Infinity Mod Tool

Infinity Mod Tool is a mod loading program that allows for the installation/uninstallation of various mods within Disney Infinity 3.0

## Upcoming Features

- Support for playsets
- Support for other types of mods

## Creating Character Mods

No actual mod files have been included in this repository, but can easily be created by adding a .json file under the `/Mods/Characters` folder in the following format (replace values with relevant ones for each character)

```
{
  "Name": "EXM_ExampleChar",
  "Sku_Id": "1000000",
  "SteamDLCAppId": "",
  "PCSKU": "",
  "WINRTSKU": "",
  "Icon": "HUD_PlayerIcons_ExampleChar",
  "Description": "EXM_ExampleChar_desc",
  "VideoLink": "Info_ExampleChar",
  "ProgressionTree": "EXM_ExampleChar",
  "CostumeCoin": "",
  "MetaData": "Examples,Franchise_EXM",
  "ReplaceCharacter": false, // Set to true if this character mod needs to replace a character in the 'locks__lua.chd' file
  "WriteToCharacterList": true, // Set to true if this character's json data needs to be written to 'virtualreaderpc_data.lua'
  "DisplayName": "Example Name", // This is the name as shown in the 
  "DisplayColor": "purple", // This affects the frames of the character portrait, ie. "red"/"purple"
  "DisplayImage": "data:image/png;base64..." // This will be displayed as the portrait image on the installation button
}
```

## Building

This tool runs in Blazor via [ElectronNet](https://github.com/ElectronNET/Electro.NET). To get this project running, please follow the steps provided in the linked ElectronNet repository.

## Additional Credits
This mod tool uses external tools, with attributions below:

[QuickBMS](https://aluigi.altervista.org/quickbms.htm) - Released under [GPL-2.0](http://www.gnu.org/licenses/old-licenses/gpl-2.0.txt)

[Unluac (.Net port)](https://github.com/HansWessels/unluac) - Released under the following [license](https://github.com/dasorik/infinity-mod-tool/blob/master/InfinityModTool/Lib/UnluacNet/UnluacNet-LICENSE.txt)
