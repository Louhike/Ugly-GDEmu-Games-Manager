# Current state
I haven't been working since a long time and I do not plan to do so in the near future. You can still use it as it is working or use https://github.com/sonik-br/GDMENUCardManager.

# Ugly GDEmu Games Manager
![alt text](./capture1.png)

## What is Ugly GDEmu Games Manager
UGGM is a Windows (for now) software to manage your games on the SD card you have for GDEmu, as it can be cumbersome.

## Where can I download it?
Here: https://github.com/Louhike/Ugly-GDEmu-Games-Manager/releases

## Features
It allows you to:
* Copy your GDI/CDI games from your PC on your SD card. It will find what must the name of folder (02,03 ,04, etc.) and create it. If an empty folder already exists, it will use it.
* GDI in archives are supported.
* Shrink (optionnaly) the games while copying them on your SD card. The files on your PC won't be shrinked.
* Remove Games from your SD card.
* Show which games on your PC are not on your SD card. The list is sortable on columns with symbols (▲, ▼ and ▬).
* Generate a menu index for GDEmu (so it does not have to analyze the SD card at launch).
* Files with stranges track names will be renamed while transfered in SD so they can work with SD Card Maker and GDEmu.
* GDI file are rewritten while copied on the SD card to improve compatibility.

More features will be added other time. You can get a glimpse on what I'm planning to work on in the Issues tab.

## How to use it
First, you must define the source(s) of your games on your PC. You must select folders containing sub-folders with one game in each. If a folder does not contain a game, it is ignored. The search is recursive, so the games can be in several levels of sub-folders. If you want to add several folders, you must can add them by separating them with a pipe character: |. Then you select the drive which contains your SD card.

When it's done, click on Scan. It will analyze all the folders/archives and display the games it find on your PC. If the game is also on your SD, it will display the corresponding folder.

In the games list, you can choose which files you cant on your SD. You can also shrink (compress) the game. Note that some games won't work with that though. Check the actions you want to apply, and then click on "Apply". You can click on "Undo" if you want to cancel the actions (please note it will do a scan).

By default, the option "Menu index" to create the index is selected as most users would want that, you can untick it though.

## Credits
The software is using the following tools made by others:
* "Extract Re-Build GDI's" by JCRocky5. License unknown but I asked for permission to use it. You can find his other projects here: https://github.com/Rocky5/
* "gditools" by FamilyGuy and Sizious. GNU General Public License version 3.0 (GPLv3). https://sourceforge.net/projects/dcisotools/
* "buildgdi" by S4pph4rad. http://projects.sappharad.com/tools/gdibuilder.html
* "SharpCompress" by adamhathcock. https://github.com/adamhathcock/sharpcompress

Without them, this software would not exist so THANKS.

Thanks to Fed (https://github.com/PapiFed) for helping testing the software and for his nice recommandations!
Thanks to gingerbeardman for his recommandations and his UI mockup for 0.7.0 version.

## License
GNU General Public License v2.0

## If you want to support the developper
__I don't need it__ but if you would like to help me pay my coffee/beer while I'm working on this, you can donate here: [![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=GU9TN9WV3PMHA&currency_code=EUR&source=url)
