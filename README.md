# RepackageAppxBundle
Ability to re package Microsoft Windows 10 Appx and Appxbundle packages

## What it is for
Reverse Engineering UWP/HoloLens application <br>
Unpackage, modify, repackage, sign an APPX <br>
Extracting .appx .appxbundle <br>
Change .appx and .appxbundle content  <br>

## Pre Requisite
Win 10 <br>
.NET FW 4.5 + 

## Build
Open solution in Visual Studio 2017 (2015 suppose to work as well) and run Build command

## How to use it?
double click the exe file, fill the AppxBundle folder in the small UI the is opened <br>
The system will extract the AppxBundle and then the Appx within it to Temp folder <br>
Then the system will open the content of the Appx folder in order to allow you to make any changes <br>
Once you are done, click OK on the small window the popued up <br>
Then the system will Re-Create the Appx and AppxBundle and sign them <br>
