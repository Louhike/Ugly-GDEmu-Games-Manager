@Echo off
mode con:cols=56 lines=15
Title Extracting %~nx1
CD %~dp0

::::::::::::::::::::::::::::::::::::::
:: Version Number & Colour
::::::::::::::::::::::::::::::
For /f "tokens=2,* delims==" %%a in ('findstr /b /i /l "Version" "Config.ini"') do Set "Version=%%a"
For /f "tokens=2,* delims==" %%a in ('findstr /b /i /l "Colour" "Config.ini"') do Color %%a
For /f "tokens=2,* delims==" %%a in ('findstr /b /i /l "MenuDelay" "Config.ini"') do Set "Delay=%%a"

::::::::::::::::::::::::::::::::::::::
:: GUI Elements.
::::::::::::::::::::::::::::::
Set "GUI_Element_1=Echo --------------------------------------------------------"
Set "GUI_Element_2=Echo ------------------------------------- Created by Rocky5"
Set "GUI_Element_3=Echo ----------------------------------------- Version %Version%"
Set "GUI_Element_4=Echo ----------------------------------------------- Error"

:Splash :D
CLS & Echo: & Echo: & Echo: & Echo:
%GUI_Element_1%
Echo  Extract GDI image.
Echo:
%GUI_Element_2%
%GUI_Element_3%
timeout /t 2 >NUL

::::::::::::::::::::::::::::::::::::::
:: Start of the main code.
::::::::::::::::::::::::::::::
:start
if "%~nx1"=="" Goto Error1
if exist "%~nx1 Extracted\bootsector\IP.BIN" Goto Error2
if not exist "%~nx1\*.gdi" Goto Error3
for /f "tokens=*" %%a in ('Dir /b "%~nx1\*.gdi"') do Set "GDI=%%a"
if exist "tools\log.txt" Del /Q "tools\Extract log.txt"


::::::::::::::::::::::::::::::::::::::
:: Extract GDI Image.
::::::::::::::::::::::::::::::
CLS & Echo: & Echo: & Echo: & Echo:
%GUI_Element_1%
Echo  Game Name = %~nx1
Echo  Game GDI File = %GDI%
Echo:
%GUI_Element_1%
Echo  Extracting game files please wait...
tools\gditools.exe -i "%~nx1\%GDI%" --data-folder "..\%~nx1 Extracted" -b "..\%~nx1 Extracted\bootsector\IP.BIN" --extract-all --silent
Echo  Extraction complete.
timeout /t %Delay% >NUL
exit



::::::::::::::::::::::::::::::::::::::
:: Errors.
::::::::::::::::::::::::::::::
:Error1
CLS & Echo: & Echo: & Echo: & Echo:
%GUI_Element_1%
Echo  Drag your DC Game folder onto this batch.
Echo:
%GUI_Element_4% 1
timeout /t 5 >NUL
Exit
:Error2
CLS & Echo: & Echo: & Echo: & Echo:
%GUI_Element_1%
Echo  This game has been extracted.
Echo:
%GUI_Element_4% 2
timeout /t 5 >NUL
Exit
:Error3
CLS & Echo: & Echo: & Echo: & Echo:
%GUI_Element_1%
Echo  Cannot find a valid .gdi file.
Echo:
%GUI_Element_4% 3
timeout /t 5 >NUL
Exit

:::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:: Usage: tools\gditools -i input_gdi [options]
:: 
::   -h, --help             Display this help
::   -l, --list             List all files in the filesystem and exit
::   -o [outdir]            Output directory. Default: gdi folder
::   -s [filename]          Create a sorttxt file with custom name
::                           (It uses *data-folder* as prefix)
::   -b [ipname]            Dump the ip.bin with custom name
::   -e [filename]          Dump a single file from the filesystem
::   --extract-all          Dump all the files in the *data-folder*
::   --data-folder [name]   *data-folder* subfolder. Default: data
::   --sort-spacer [num]    sorttxt entries are sperated by num
::                            (__volume_label__ --> Use ISO9660 volume label)
::  >>tools\log.txt               Minimal verbosity mode
::   [no option]            Display gdi infos if not silent
:: 
:: 
:: gditools.py by FamilyGuy, http://sourceforge.net/p/dcisotools/
::     Licensed under GPLv3, see licences folder.
:: 
:: iso9660.py  by Barney Gale, http://github.com/barneygale
::     Licensed under a BSD-based license, see licences folder.
:::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::