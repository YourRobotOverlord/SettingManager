# SettingManager

## Overview
This project was created with the intention of building a very simple setting manager for my own use that is extremely easy to use and avoids some of the pitfalls of the .Net settings offerings.
All settings are serialized as XML, allowing any serializable object to be saved to the hive. This adds a bit of size, but for most projects this is not an issue at all.
SettingsManager does not utilize SQLite transactions to keep things simple so it will not win any points for speed.
SettingManager automagically handles the creation of settings hives using SQLite and Dapper. These hives are created as needed at one or more of the following locations:
* CommonApplicationData (i.e. c:\ProgramData\)
* ApplicationData (i.e. c:\users\currentuser\AppData\Roaming\)
* LocalApplicationData (i.e. c:\users\currentuser\AppData\Local\)

## Prefixes

SettingManager uses naming convention to determine the correct hive location to use.
A prefix is used for this purpose, and can be one of the following:

Prefix  |   Storage Location Example
------------------------------------------------------------------------
@ru     |   c:\Users\currentuser\AppData\Roaming\Domain\Application Name
@lu     |   c:\Users\currentuser\AppData\Local\Domain\Application Name
@ap     |   c:\ProgramData\Domain\Application Name
none    |   c:\Users\currentuser\AppData\Local\Domain\Application Name

## Usage Example

```c#
    //On my machine this will save settings to c:\ProgramData\M3Logic\Test App\Settings.db
    SettingManager settings = new SettingManager("M3Logic", "Test App", "Settings.db");

    settings.SaveSetting<string>("@apTestStringSetting", "Test String");

    Artifact pricelessArtifact = new Artifact { Name = "Stone Idol", Value = 500.00M };
    settings.SaveSetting<Artifact>("@apTestArtifactSetting", pricelessArtifact);

    //Get settings
    Console.WriteLine(settings.GetSetting<string>("@apTestStringSetting"));

    //Same thing, but with an optional default value:
    Console.WriteLine(settings.GetSetting<string>("@apTestStringSetting","Test string"));

    Console.WriteLine(settings.GetSetting<Artifact>("@apTestArtifactSetting").Name);
```
Refer to the SettingManagerTest for more usage examples.