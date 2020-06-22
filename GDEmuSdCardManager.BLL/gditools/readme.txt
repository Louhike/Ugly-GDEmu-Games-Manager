                                ___ __              __    
                     ____ _____/ (_) /_____  ____  / /____
                    / __ `/ __  / / __/ __ \/ __ \/ / ___/
                   / /_/ / /_/ / / /_/ /_/ / /_/ / (__  ) 
                   \__, /\__,_/_/\__/\____/\____/_/____/  
                  /____/

    gditools, a Python library to extract files, sorttxt.txt and 
    bootsector (IP.BIN) from SEGA Gigabyte Disc (GD-ROM) dumps in 
    gdi format.

    The goals is to make it efficient, readable and multi-platform.

    As of December 2014 it's tested working on Linux and Win7 under 
    python 2.7 but it was only tested on x86_64 processors. The 
    performances are typically limited by the usage of a platter HDD.
    When using a SSD, the CPU can be the bottleneck if it's an old one
    or if it's used in power-saving mode. 3-tracks gdi can be extracted
    in less than 2 seconds with the right configuration (~1GiB of data).

    To get the most recent version, browse code on:
    https://sourceforge.net/projects/dcisotools/

    or using git: 
    git clone git://git.code.sf.net/p/dcisotools/code dcisotools-code

    Releases of stable code might be sporadically packaged into a .zip
    archive for convenience, and made available on the SourceForge page
    download section.
    
    bin2iso.py and gdifix.py (creating a single .iso from a gdi dump)
    are provided in the 'addons' folder. They can be used as is or be
    used to see how to incorporate gditools.py in another project.

    See the Legal Stuff section at the end of this readme for the infos
    on licensing and using this project in another one.

    Enjoy!    
      
    FamilyGuy 2015

    Thanks to SiZiOUS for testing the code, providing support and for the GUI.

     ___                _                        __    
    / _ \___ ___ ___ __(_)______ __ _  ___ ___  / /____
___/ , _/ -_) _ `/ // / / __/ -_)  ' \/ -_) _ \/ __(_-<________________________
  /_/|_|\__/\_, /\_,_/_/_/  \__/_/_/_/\__/_//_/\__/___/
             /_/
			 
   - Python 2.7.x, Python 3 won't work.
   - On Windows you have to add the python folder to your path manually (or 
     choose the option when installing).

     __  __                 
    / / / /__ ___ ____ ____ 
___/ /_/ (_-</ _ `/ _ `/ -_)___________________________________________________
   \____/___/\_,_/\_, /\__/ 
                 /___/      
    python gditools.py -i input_gdi [options]

      -h, --help             Display this help
      -l, --list             List all files in the filesystem and exit
      -o [outdir]            Output directory. Default: gdi folder
      -s [filename]          Create a sorttxt file with custom name
                               (It uses *data-folder* as prefix)
      -b [ipname]            Dump the ip.bin with custom name
      -e [filename]          Dump a single file from the filesystem
      --extract-all          Dump all the files in the *data-folder*
      --data-folder [name]   *data-folder* subfolder. Default: data
                               (__volume_label__ --> Use ISO9660 volume label)
      --sort-spacer [num]    Sorttxt entries are sperated by num
      --silent               Minimal verbosity mode
      [no option]            Display gdi infos if not silent

     __  __                    ____                     __      
    / / / /__ ___ ____ ____   / __/_ _____ ___ _  ___  / /__ ___
___/ /_/ (_-</ _ `/ _ `/ -_)_/ _/ \ \ / _ `/  ' \/ _ \/ / -_|_-<_______________
   \____/___/\_,_/\_, /\__/ /___//_\_\\_,_/_/_/_/ .__/_/\__/___/
                 /___/                         /_/              
			  
  0- Listing all files in the gdi:
        gditools.py -i /folder/disc.gdi --list

  1- Displaying gdi infos:
        gditools.py -i /folder/disc.gdi

  2- Dumping bootsector/initial program/ip.bin:
        gditools.py -i /folder/disc.gdi -b ip.bin

  3- Generating a sorttxt file:
        gditools.py -i /folder/disc.gdi -s sorttxt.txt

  4- Generating a sorttxt file with a different "data" folder (see example 9):
        gditools.py -i /folder/disc.gdi -s sorttxt.txt --data-folder MyDump

  5- Extracting a single file:
        gditools.py -i /folder/disc.gdi -e 1st_read.bin

  6- Specifying a different output folder:
       (default one is the gdi folder)
        gditools.py -i /folder/disc.gdi -e 1st_read.bin -o /OtherFolder

  7- Extracting all the files from the gdi:
        gditools.py -i /folder/disc.gdi --extract-all

  8- Specifying a different subfolder name:
       (default one is "data")
        gditools.py -i /folder/disc.gdi --extract-all --data-folder MyFolder

  9- Using the iso9660 filesystem volume label as the subfolder name:
        gditools.py -i /folder/disc.gdi --extract-all --data-folder __volume_label__

 10- Doing most of the above at once:
        gditools.py -i /folder/disc.gdi -s sorttxt.txt -b ip.bin 
                    -o /OtherFolder --data-folder __volume_label__  --extract-all

     __  __    _             __  __         _______  ______
    / / / /__ (_)__  ___ _  / /_/ /  ___   / ___/ / / /  _/
___/ /_/ (_-</ / _ \/ _ `/ / __/ _ \/ -_) / (_ / /_/ // /______________________
   \____/___/_/_//_/\_, /  \__/_//_/\__/  \___/\____/___/  
                   /___/                                   
				   
    For your convenience you can use the GUI provided for your platform.

    To use it it's really simple:
      1. Download the package for your platform (Windows, Linux 64-bit or 
         Mac OS X).
      2. Extract the GUI binary at the same location of your gditools.py script.
      3. Just double-click on the 'gditools.exe' or 'gditools' binary to run it.

    The usage is pretty much the same as the excellent GD-ROM Explorer made by
    Japanese Cake which is only available on Windows.

    If you want to modify/compile the GUI for your platform, please use the
    Lazarus IDE: http://www.lazarus.freepascal.org/
	
    Please note this procedure is also included in the gditools-gui-* packages
    into the 'setupgui.txt' file.
      __                 __  ______       ______
     / /  ___ ___ ____ _/ / / __/ /___ __/ _/ _/
___ / /__/ -_) _ `/ _ `/ / _\ \/ __/ // / _/ _/________________________________
   /____/\__/\_, /\_,_/_/ /___/\__/\_,_/_//_/   
            /___/                               

    gditools.py, provided addons and the GUI are licensed under the GNU
    General Public License (version 3), a copy of which is provided
    in the licences folder: GNU_GPL_v3.txt
    
    Original iso9660.py by Barney Gale : github.com/barneygale/iso9660
    iso9660.py is licensed under a BSD license, a copy of which is 
    provided in the licences folder: iso9660_license.txt

_____________________________________________________________________/ eof /___
