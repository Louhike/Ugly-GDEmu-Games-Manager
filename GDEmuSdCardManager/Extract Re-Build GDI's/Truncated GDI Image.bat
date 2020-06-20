@Echo off
mode con:cols=56 lines=15
Title %~nx1
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
Echo  Truncate GDI image.
Echo:
%GUI_Element_2%
%GUI_Element_3%
timeout /t 2 >NUL

::::::::::::::::::::::::::::::::::::::
:: Start of the main code.
::::::::::::::::::::::::::::::
:start
Set "Game_Folder=%~nx1"

if "%~nx1"=="" Goto Error1
if not exist "%Game_Folder%\*.gdi" Goto Error4
CD %Game_Folder%
for /f "Tokens=*" %%a in ('dir /b "*.gdi"') do Set "GDI=%%a"


::::::::::::::::::::::::::::::::::::::
:: Checks if cdda audio exists.
::::::::::::::::::::::::::::::
if not exist "Track04.raw" Goto no_cdda


::::::::::::::::::::::::::::::::::::::
:: Gets the total .raw count.
::::::::::::::::::::::::::::::
for /f "Tokens=*" %%a in ('dir /b "Track*.raw" ^| find "Track*.raw" /v /n /c') do Set "RAWTotal=%%a"
Set /a RAWCount=3
Set /a Count2Total=1


::::::::::::::::::::::::::::::::::::::
:: Loop to process all .raw files.
::::::::::::::::::::::::::::::
:Loop
Set /a Count2Total+=1
Set /a RAWCount+=1
If %RAWCount% LSS 10 Set "TRACK0%RAWCount%=TRACK0%RAWCount%.raw"
If %RAWCount% GTR 9 Set "TRACK%RAWCount%=TRACK%RAWCount%.raw"
::If %RAWCount% LSS 10 Echo TRACK0%RAWCount%.raw
::If %RAWCount% GTR 9 Echo TRACK%RAWCount%.raw
If Not "%RAWTotal%"=="%Count2Total%" Goto Loop
Set /a RAWCount-=3


::::::::::::::::::::::::::::::::::::::
:: Builds an image using cdda audio.
::::::::::::::::::::::::::::::
CLS & Echo: & Echo: & Echo:
%GUI_Element_1%
Echo  Game Name = %Game_Folder%
Echo  Game GDI File = %GDI%
Echo  Total CDDA Tracks = %RAWCount%
Echo:
%GUI_Element_1%
Echo  Truncating image please wait...
..\tools\gditools.exe -i "..\%~nx1\%GDI%" --data-folder "Extracted" -b "Extracted\bootsector\IP.BIN" --extract-all --silent
..\tools\buildgdi.exe -data "..\%~nx1\Extracted" -ip "..\%~nx1\Extracted\bootsector\IP.BIN" -output "..\%~nx1" -gdi "%GDI%" -raw -truncate -cdda %TRACK04% %TRACK05% %TRACK06% %TRACK07% %TRACK08% %TRACK09% %TRACK10% %TRACK11% %TRACK12% %TRACK13% %TRACK14% %TRACK15% %TRACK16% %TRACK17% %TRACK18% %TRACK19% %TRACK20% %TRACK21% %TRACK22% %TRACK23% %TRACK24% %TRACK25% %TRACK26% %TRACK27% %TRACK28% %TRACK29% %TRACK30% %TRACK31% %TRACK32% %TRACK33% %TRACK34% %TRACK35% %TRACK36% %TRACK37% %TRACK38% %TRACK39% %TRACK40% %TRACK41% %TRACK42% %TRACK43% %TRACK44% %TRACK45% %TRACK46% %TRACK47% %TRACK48% %TRACK49% %TRACK50% %TRACK51% %TRACK52% %TRACK53% %TRACK54% %TRACK55% %TRACK56% %TRACK57% %TRACK58% %TRACK59% %TRACK60% %TRACK61% %TRACK62% %TRACK63% %TRACK64% %TRACK65% %TRACK66% %TRACK67% %TRACK68% %TRACK69% %TRACK70% %TRACK71% %TRACK72% %TRACK73% %TRACK74% %TRACK75% %TRACK76% %TRACK77% %TRACK78% %TRACK79% %TRACK80% %TRACK81% %TRACK82% %TRACK83% %TRACK84% %TRACK85% %TRACK86% %TRACK87% %TRACK88% %TRACK89% %TRACK90% %TRACK91% %TRACK92% %TRACK93% %TRACK94% %TRACK95% %TRACK96% %TRACK97% %TRACK98% %TRACK99% %TRACK100% >NUL
RD /S /Q "Extracted" >NUL
Echo  Truncating complete.
timeout /t %Delay% >NUL
exit


::::::::::::::::::::::::::::::::::::::
:: Builds a non cdda image.
::::::::::::::::::::::::::::::
:no_cdda
CLS & Echo: & Echo: & Echo:
%GUI_Element_1%
Echo  Game Name = %Game_Folder%
Echo  Game GDI File = %GDI%
Echo  No CDDA Tracks
Echo:
%GUI_Element_1%
Echo  Truncating image please wait...
..\tools\gditools.exe -i "..\%~nx1\%GDI%" --data-folder "Extracted" -b "Extracted\bootsector\IP.BIN" --extract-all --silent
..\tools\buildgdi.exe -data "..\%~nx1\Extracted" -ip "..\%~nx1\Extracted\bootsector\IP.BIN" -output "..\%~nx1" -gdi "%GDI%" -raw -truncate >NUL
RD /S /Q "Extracted" >NUL
Echo  Truncating complete.
timeout /t %Delay% >NUL
exit


::::::::::::::::::::::::::::::::::::::
:: Errors.
::::::::::::::::::::::::::::::
:Error1
CLS & Echo: & Echo: & Echo: & Echo:
%GUI_Element_1%
Echo  Drag your extracted DC Game folder onto this batch.
Echo:
%GUI_Element_4% 1
timeout /t 5 >NUL
Exit
:Error2
CLS & Echo: & Echo: & Echo: & Echo:
%GUI_Element_1%
Echo  This is not an extracted game folder.
Echo:
%GUI_Element_4% 2
timeout /t 5 >NUL
Exit
:Error3
CLS & Echo: & Echo: & Echo: & Echo:
%GUI_Element_1%
Echo  Bootsector\IP.bin is missing.
Echo:
%GUI_Element_4% 3
timeout /t 5 >NUL
Exit
:Error4
CLS & Echo: & Echo: & Echo: & Echo:
%GUI_Element_1%
Echo  Cannot find a valid .gdi file.
Echo:
%GUI_Element_4% 4
timeout /t 5 >NUL
Exit


::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:: BuildGDI - Command line GDIBuilder
:: Usage: buildgdi -data dataFolder -ip IP.BIN -cdda track04.raw track05.raw -output folder -gdi disc.gdi
:: 
:: Arguments:
:: -data <folder> (Required) = Location of the files for the disc
:: -ip <file> (Required) = Location of disc IP.BIN bootsector
:: -cdda <files> (Optional) = List of RAW CDDA tracks on the disc
:: -output <folder or file(s)> (Required) = Output location
::    If output is a folder, tracks with default filenames will be generated.
::    Otherwise, specify one filename for track03.bin on data only discs, 
::    or two files for discs with CDDA.
:: -gdi <file> (Optional) = Path of the disc.gdi file for this disc.
::    Existing GDI files will be updated with the new tracks.
::    If no GDI exists, only lines for tracks 3 and above will be written.
:: -V <volume identifier> (Optional) = The volume name (Default is DREAMCAST)
:: -raw (Optional) = Output 2352 byte raw disc sectors instead of 2048.
:: -truncate (Optional) = Do not pad generated data to the correct size.
::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
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
::   --silent               Minimal verbosity mode
::   [no option]            Display gdi infos if not silent
:: 
:: 
:: gditools.py by FamilyGuy, http://sourceforge.net/p/dcisotools/
::     Licensed under GPLv3, see licences folder.
:: 
:: iso9660.py  by Barney Gale, http://github.com/barneygale
::     Licensed under a BSD-based license, see licences folder.
:::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::