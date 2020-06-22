#!/usr/bin/python
# -*- coding: utf-8 -*-

"""
    gditools, a python library to extract files, sorttxt.txt and 
    bootsector (ip.bin) from SEGA Gigabyte Disc (GD-ROM) dumps.
    
    FamilyGuy 2014-2015
        
    
    gditools.py and provided examples are licensed under the GNU
    General Public License (version 3), a copy of which is provided
    in the licences folder: GNU_GPL_v3.txt

    
    Original iso9660.py by Barney Gale : github.com/barneygale/iso9660
    iso9660.py is licensed under a BSD license, a copy of which is 
    provided in the licences folder: iso9660_licente.txt
"""

import os, sys, shutil, getopt
from copy import deepcopy
from iso9660 import ISO9660 as _ISO9660_orig
from struct import unpack
from datetime import datetime
try:
    from cStringIO import StringIO
except ImportError:
    from StringIO import StringIO


# TODO TODO TODO
#
#   - Uniformize how we create unexisting paths exists 
#          (files/sort/bootsector/outputpath)
#
#   - Test extensively, find bugs and fix them?
#
# TODO TODO TODO



class ISO9660(_ISO9660_orig):
    """
    Modification to iso9660.py to easily handle GDI files 
    
    """
    
    ### Overriding Functions of original class in this section

    def __init__(self, gdi, *args, **kwargs):
        # We obviously override the init to add support for our modifications
        self._gdi = gdi

        self._dict1 = [i for i in gdi if i['lba']==45000][0] # That's ugly but it works
        self._dirname = os.path.dirname(self._dict1['filename'])
        self._dict2 = None
        if len(gdi) > self._dict1['tnum']:
                self._dict2 = gdi[-1]
                
        self._last_read_toc_sector = 0  # Last read TOC sector *SO FAR*

        self._gdifile = AppendedFiles(self._dict1, self._dict2)

        _ISO9660_orig.__init__(self, 'url') # So url doesn't starts with http

        if kwargs.has_key('verbose'):
            self._verbose = kwargs.pop('verbose')
        else:
            self._verbose = False


    
    ### Overriding this function allows to parse AppendedFiles as isos
    
    def _get_sector_file(self, sector, length): 
        # A big performance improvement versus re-opening the file for each
	    # read as in the original ISO9660 implementation.
        self._gdifile.seek(sector*2048)
        self._buff = StringIO(self._gdifile.read(length))

    def _unpack_record(self, read=0):
        tmp = _ISO9660_orig._unpack_record(self, read)
        pointer = self._gdifile.tell()
        # Where are we in sectors?
        current_pointer_pos = pointer//2048
        # If we're not at the exact beginning of a sector, we add one.
        if pointer%2048:
            current_pointer_pos += 1

        if current_pointer_pos > self._last_read_toc_sector:
            self._last_read_toc_sector = current_pointer_pos
            
        return tmp
        


    ### NEW FUNCTIONS FOLLOW ###

    def get_record(self, path):
        path = path.upper().strip('/').split('/')
        path, filename = path[:-1], path[-1]

        if len(path)==0:
            parent_dir = self._root
        else:
            parent_dir = self._dir_record_by_root(path)

        f = self._search_dir_children(parent_dir, filename)
        return f


    def gen_records(self, get_files = True):
        gen = self._tree_nodes_records(self._root)
        for i in gen:
            if get_files:
                yield i
            elif i['flags'] == 2:
                yield i 


    def _tree_nodes_records(self, node):
        spacer = lambda s: dict(
                    {j:s[j] for j in [i for i in s if i != 'name']}.items(),
                    name = "%s/%s" % (node['name'].lstrip('\x00\x01'), 
                    s['name']))
        for c in list(self._unpack_dir_children(node)):
            yield spacer(c)
            if c['flags'] & 2:
                for d in self._tree_nodes_records(c):
                    yield spacer(d)

    def get_pvd(self):
        return self._pvd

    def get_volume_label(self):
        return self.get_pvd()['volume_identifier']

    def print_files(self):
        for i in self.tree():
            print(i)


    def get_bootsector(self, lba = 45000):
        self._get_sector(lba, 16*2048)
        return self._unpack_raw(16*2048)


    def get_file_by_record(self, filerec):
        self._gdifile.seek(filerec['ex_loc']*2048)
        return self._gdifile.read(filerec['ex_len'])

    def get_file(self, filename):
        return self.get_file_by_record(self.get_record(filename))


    def get_sorttxt(self, crit='ex_loc', prefix='data', dummy='0.0', spacer=1):
        """
        prefix : Folder that will be created in the pwd.
                 Default: 'data'

        dummy : Name of the dummy file to be put in the sorttxt
                Set to False not to use a dummy file
                Default: '0.0'

        crit : (criterion) can be any file record entry
               Default: 'ex_loc'    (LBA)

        If the first letter of crit is uppercase, order is reversed

        e.g.

        'ex_loc' or 'EX_LOC'    ->    Sorted by LBA value.
        'name' or 'NAME'        ->    Sorted by file name.
        'ex_len' or 'EX_LEN'    ->    Sorted by file size.
        

        Note: First file in sorttxt represents the last one on disc.

        e.g.

        - A sorttxt representing the file order of the source iso:
            self.get_sorted(crit='ex_loc')

        - A sorttxt with BIGGEST files at the outer part of disc:
            self.get_sorted(crit='ex_len')

        - A sorttxt with SMALLEST files at the outer part of disc:
            self.get_sorted(criterion='EX_LEN')
        """
        return self._sorttxt_from_records(self._sorted_records(crit=crit),
                                     prefix=prefix, dummy=dummy, spacer = spacer)


    def _sorted_records(self, crit='ex_loc', nodirs=True):
        file_records = [i for i in self.gen_records()]
        if nodirs:
            for i in self.gen_records(get_files = False):
                file_records.pop(file_records.index(i))  # Strips directories
        reverse = crit[0].islower()
        crit = crit.lower()
        ordered_records = sorted(file_records, key=lambda k: k[crit], 
                                 reverse = reverse)
        return ordered_records


    def _sorttxt_from_records(self, records, prefix='data', dummy='0.0', spacer = 1):
        spacer = int(spacer)
        sorttxt=''
        newline = '{prefix}{filename} {importance}\r\n'
        for i,f in enumerate(records):
            sorttxt += newline.format(prefix=prefix, filename=f['name'],
                                      importance = spacer*(i+1))
        if dummy:
            if not dummy[0] == '/': 
                dummy = '/' + dummy
            sorttxt += newline.format(prefix=prefix, filename=dummy,
                                      importance = spacer*(len(records)+1))
        return sorttxt


    def dump_sorttxt(self, filename='sorttxt.txt', **kwargs):
        if not filename[0] == '/': # Paths rel. to gdi folder unless full paths
            filename = self._dirname + '/' + filename

        path = os.path.dirname(filename)
        if not os.path.exists(path):
            # Creates required dirs, including empty ones
            os.makedirs(path)   
            if self._verbose: 
                message = 'Created directory: {}'
                UpdateLine(message.format(path))

        with open(filename, 'wb') as f:
            if self._verbose: 
                print('Dumping sorttxt to {}'.format(filename))
            f.write(self.get_sorttxt(**kwargs))

    def dump_bootsector(self, filename='ip.bin', **kwargs):
        if not filename[0] == '/': # Paths rel. to gdi folder unless full paths
            filename = self._dirname + '/' + filename

        path = os.path.dirname(filename)
        if not os.path.exists(path):
            # Creates required dirs, including empty ones
            os.makedirs(path)   
            if self._verbose: 
                message = 'Created directory: {}'
                UpdateLine(message.format(path))

        with open(filename, 'wb') as f:
            if self._verbose: 
                print('Dumping bootsector to {}'.format(filename))
            f.write(self.get_bootsector(**kwargs))

    def dump_file_by_record(self, rec, target = '.', keep_timestamp = True,
                            filename = None):
        """
        rec: Record of a file in the filesystem
        target: Directory target to dump file into
        keep_timestamp: Uses timestamp in fs for dumped file
        filename: *None* -> Uses name in fs, else it overrides filename
        """
        if not target[-1] == '/': target += '/'
        # User provided filename overrides records's subfolders & name
        if filename:
            filename = target + filename.strip('/') 
        else: 
            filename = target + rec['name'].strip('/')

        if rec['flags'] == 2:
            # So os.path.isdirname yields right value for dir records
            filename += '/' 
        
        path = os.path.dirname(filename)
        if not os.path.exists(path):
            # Creates required dirs, including empty ones
            os.makedirs(path)   
            if self._verbose: 
                message = 'Created directory: {}'
                UpdateLine(message.format(path))

        if rec['flags'] != 2:   # If rec doesn't represents a directory
            message = 'Dumping {} to {}    ({}, {})'
            if self._verbose: 
                UpdateLine(message.format(rec['name'].split('/')[-1],
                                          filename, rec['ex_loc'],
                                          rec['ex_len']))
            with open(filename, 'wb') as f:
                # Using buffered copy to speed up things, hopefully
                # Potentially beneficial on Windows mainly
                self._gdifile.seek(rec['ex_loc']*2048)
                _copy_buffered(self._gdifile, f, length = rec['ex_len'])

            if keep_timestamp:
                os.utime(filename, (self._get_timestamp_by_record(rec),)*2)


    def dump_file(self, name, **kwargs):
        self.dump_file_by_record(self.get_record(name), **kwargs)
        if self._verbose:
            UpdateLine('\n')


    def dump_all_files(self, target='data', **kwargs): 
        # target has a default value not to accidentally fill dev folder 
        # Sorting according to LBA to avoid too much skipping on HDDs

        if not target[0] == '/': # Paths rel. to gdi folder unless full paths
            target = self._dirname + '/' + target
        try:
            for i in self._sorted_records(crit='EX_LOC', nodirs=False):
                self.dump_file_by_record(i, target = target, **kwargs)

            if self._verbose:
                UpdateLine('All files were dumped successfully.')
                UpdateLine('\n')

        except:
            if self._verbose:
                UpdateLine('There was an error dumping all files.')


    def get_time_by_record(self, rec):
        tmp = datetime.fromtimestamp(self._get_timestamp_by_record(rec))
        return tmp.strftime('%Y-%m-%d %H:%M:%S (localtime)')


    def get_time(self, filename):
        return self.get_time_by_record(self.get_record(filename))


    def _get_timestamp_by_record(self, rec):
        date = rec['datetime']
        t = [unpack('<B', i)[0] for i in date[:-1]]
        t.append(unpack('<b', date[-1])[0])
        t[0] += 1900
        t_timestamp = self._datetime_to_timestamp(t)
        return t_timestamp
    
    def _datetime_to_timestamp(self, t):
        epoch = datetime(1970, 1, 1)
        timez = t.pop(-1) * 15 * 60. 
        # timez: Offset from GMT in 15 min intervals converted to secs
        T = (datetime(*t)-epoch).total_seconds()
        return T - timez


    def get_last_toc_sector(self):
        # Parsing the whole TOC to find the last accessed sector.
        tmp = map(None, self.tree(), self.tree(get_files=False))
        return self._last_read_toc_sector

    def get_first_file_sector(self):
        # Some FS include DA tracks in their FS, it should be ignored here
        i=-1
        lba = self._sorted_records()[i]['ex_loc']
        while lba < self._gdi[-1]['lba']:
            i -= 1
            lba = self._sorted_records()[i]['ex_loc']
        return lba

    def shrink_blank_check(self):
        gdifile = self._gdifile
        begin = self.get_last_toc_sector()
        end = self.get_first_file_sector()
        
        length = end-begin
        readsize = 512  # Sectors 512 -> 1 MiB at a time
        sectref = "\x00"*2048
        readref = readsize*sectref
        nz_sect = [begin,]    # Non-zero sectors between *begin* and *end*

        for i in xrange(length//readsize):
            gdifile.seek((begin+i*readsize)*2048) # Make sure we're at right offset
            if not readref == gdifile.read(readsize*2048):
                gdifile.seek(-1*readsize*2048, 1)   # Rewind
                for j in xrange(readsize):
                    if not sectref == gdifile.read(2048):
                        nz_sect.append(begin+i*readsize+j) # Current sector

        for i in xrange(length%readsize):
            gdifile.seek((begin+length//readsize*readsize+i)*2048) # Make sure we're at right offset
            if not sectref == gdifile.read(2048):
                nz_sect.append(begin+i*readsize+j) # Current sector
        
        nz_sect.append(end) # Previously found end is non-zero

        # Finding the largest zero-filled gap
        gaps = [j-i for i,j in zip(nz_sect[:-1],nz_sect[1:])]
        gidx = gaps.index(max(gaps))

        # Setting new begin/end to the largest zero-filled gap
        begin = nz_sect[gidx] # gidx+1 is first zero since gdix is non-zero, 
        end = nz_sect[gidx+1]   # 

        return begin, end


    def __enter__(self):
        return self

    def __exit__(self, type=None, value=None, traceback=None):
        self._gdifile.__exit__()


class GDIfile(ISO9660): 
    """
    Returns a class that represents a gdi dump of a GD-ROM.
    It should be initiated with a string pointing to a gdi file.

    Boolean kwarg *verbose* enables printing infos on what's going on.

    e.g.
    gdi = gdifile('disc.gdi')
    gdi.dump_all_files()
    """
    def __init__(self, filename, **kwargs): # Isn't OO programming wonderful?
        verbose = kwargs['verbose'] if kwargs.has_key('verbose') else False 
        ISO9660.__init__(self, parse_gdi(filename, verbose=verbose), **kwargs)
        self._gdi_filename = filename

    def __enter__(self):
        return self

    def __exit__(self, type=None, value=None, traceback=None):
        self._gdifile.__exit__()
        




class CdImage(file):
    """
    Class that allows opening a 2352, 2336 or 2048 bytes/sector data cd track
    as a 2048 bytes/sector one.
    """
    def __init__(self, filename, mode = 'auto', manualRawOffset=0, *args, **kwargs):

        if mode == 'auto':
            if filename[-4:] == '.iso': mode = 2048
            elif filename[-4:] == '.bin': mode = 2352

        elif not mode in [2048, 2352, 2336]:
            raise ValueError('Argument mode should be either 2048 or 2352')
        self.__mode = mode
        
        if mode == 2352:
            self._sectorOffset = 16
            self._skipToNext = 304
            
        elif mode == 2336:
            self._sectorOffset = 8
            self._skipToNext = 288
            
        self.__manualRawOffset = manualRawOffset

        if (len(args) > 0) and (args[0] not in ['r','rb']):
            raise NotImplementedError('Only read mode is implemented.')

        file.__init__(self, filename, 'rb')

        file.seek(self,0,2)
        if self.__mode in [2352, 2336]:
            self.length = file.tell(self) * 2048/self.__mode
        else:
            self.length = file.tell(self)
        file.seek(self,0,0)

        self.seek(0)

    def realOffset(self,a):
        return a/2048*self.__mode + a%2048 + self._sectorOffset + self.__manualRawOffset

    def seek(self, a, b = 0):
        if self.__mode == 2048:
            file.seek(self, a, b)

        elif self.__mode in [2352, 2336]:
            if b == 0:
                self.binpointer = a
            if b == 1:
                self.binpointer += a
            if b == 2:
                self.binpointer = self.length - a

            realpointer = self.realOffset(self.binpointer)
            file.seek(self, realpointer, 0)

    def read(self, length = None):
        if self.__mode == 2048:
            return file.read(self, length)

        elif self.__mode in [2352, 2336]:
            if length == None:
                length = self.length - self.binpointer

            # Amount of bytes left until beginning of next sector
            tmp = 2048 - self.binpointer % 2048    
            FutureOffset = self.binpointer + length
            realLength = self.realOffset(FutureOffset) - \
                            self.realOffset(self.binpointer)
            # This will (hopefully) accelerates readings on HDDs at the
            # cost of more memory use.
            buff = StringIO(file.read(self, realLength)) 
            # The first read can be < 2048 bytes
            data = buff.read(tmp)
            length -= tmp
            buff.seek(self._skipToNext, 1)
            # The middle reads are all 2048 so we optimize here!
            for i in xrange(length / 2048):
                data += buff.read(2048)
                buff.seek(self._skipToNext, 1)
            # The last read can be < 2048 bytes
            data += buff.read(length % 2048)
            # Seek back to where we should be
            self.seek(FutureOffset)
            return data

    def tell(self):
        if self.__mode == 2048:
            return file.tell(self)

        elif self.__mode in [2352, 2336]:
            return self.binpointer



class OffsetedFile(CdImage):
    """
    Like a file, but offsetted! Padding is made of 0x00.

    READ ONLY: trying to open a file in write mode will raise a 
    NotImplementedError
    """
    def __init__(self, filename, *args, **kwargs):

        if kwargs.has_key('offset'):
            self.offset = kwargs.pop('offset')
        else:
            self.offset = 0

        if (len(args) > 0) and (args[0] not in ['r','rb']):
            raise NotImplementedError('Only read mode is implemented.')

        CdImage.__init__(self, filename, **kwargs)
        
        CdImage.seek(self,0,2)
        self.length = CdImage.tell(self)
        CdImage.seek(self,0,0)

        self.seek(0)


    def seek(self, a, b = 0):
        if b == 0:
            self.pointer = a
        if b == 1:
            self.pointer += a
        if b == 2:
            self.pointer = self.length + self.offset - a

        if self.pointer > self.offset:
            CdImage.seek(self, self.pointer - self.offset)
        else:
            CdImage.seek(self, 0)


    def read(self, length = None):
        if length == None:
            length = self.offset + self.length - self.pointer
        tmp = self.pointer
        FutureOffset = self.pointer + length
        if tmp >= self.offset:
            #print 'AFTER OFFSET'
            self.seek(tmp)
            data = CdImage.read(self, length)
        elif FutureOffset < self.offset:
            #print 'BEFORE OFFSET'
            data = '\x00'*length
        else:
            #print 'CROSSING OFFSET'
            preData = '\x00'*(self.offset - tmp)
            self.seek(self.offset)
            postData = CdImage.read(self, FutureOffset - self.offset)
            data = preData + postData
        self.seek(FutureOffset)
        return data


    def tell(self):
        return self.pointer



class WormHoleFile(OffsetedFile):
    """
    Redirects an offset-range to another offset in a file. Because 
    everbody likes wormholes. 

    I even chose that name before WH were mainsteam (Interstellar)
    """
    def __init__(self, *args, **kwargs):

        # *wormhole* should be [target_offset, source_offset, wormlen]
        # target_offset + wormlen < source_offset
        
        if kwargs.has_key('wormhole'):
            self.target, self.source, self.wormlen = kwargs.pop('wormhole')
        else:
            self.target, self.source, self.wormlen = [0,0,0]

        OffsetedFile.__init__(self, *args, **kwargs)


    def read(self, length = None):

        if length == None:
            length = self.offset + self.length - self.pointer
        tmp = self.pointer
        FutureOffset = self.pointer + length

        # If we start after the wormhole or if we don't reach it, 
        # everything is fine
        if (tmp >= self.target + self.wormlen) or (FutureOffset < self.target):
            # print 'OUT OF WORMHOLE'
            data = OffsetedFile.read(self, length)

        # If we start inside the wormhole, it's trickier        
        elif tmp >= self.target:
            # print 'START INSIDE'
            # Through the wormhole to the source
            self.seek(tmp - self.target + self.source)  

            # If we don't exit the wormhole, it's somewhat simple
            if FutureOffset < self.target + self.wormlen: 
                # print 'DON\'T EXIT IT'
                data = OffsetedFile.read(self, length) # Read in the source

            # If we exit the wormhole midway, it's even trickier
            else:   
                # print 'EXIT IT'
                inWorm_len = self.target + self.wormlen - tmp
                outWorm_len = FutureOffset - self.target - self.wormlen
                inWorm = OffsetedFile.read(self, inWorm_len)
                self.seek(self.target + self.wormlen)
                outWorm = OffsetedFile.read(self, outWorm_len)
                data = inWorm + outWorm

        # If we start before the wormhole then hop inside, it's also 
        # kinda trickier
        elif FutureOffset < self.target + self.wormlen: 
            # print 'START BEFORE, ENTER IT'
            preWorm_len = self.target - tmp
            inWorm_len = FutureOffset - self.target
            preWorm = OffsetedFile.read(self, preWorm_len)
            self.seek(self.source)
            inWorm = OffsetedFile.read(self, inWorm_len)
            data = preWorm + inWorm

        # Now if we start before the wormhole and jump over it, it's 
        # the trickiest
        elif FutureOffset > self.target + self.wormlen:
            # print 'START BEFORE, END AFTER'
            preWorm_len = self.target - tmp
            inWorm_len = self.wormlen
            postWorm_len = FutureOffset - self.target - self.wormlen

            preWorm = OffsetedFile.read(preWorm_len)
            self.seek(self.source)
            inWorm = OffsetedFile.read(inWorm_len)
            self.seek(self.target + inWorm_len)
            postWorm = OffsetedFile.read(postWorm_len)

            data = preWorm + inWorm + postWorm
        

        # Pretend we're still where we should, in case we went where we
        # shouldn't have!
        self.seek(FutureOffset)     
        return data



class AppendedFiles():
    """
    Two WormHoleFiles one after another. 
    Takes 1 or 2 dict(s) as arguments; they're passed to WormHoleFiles'
    at the init.

    This is aimed at merging the TOC track starting at LBA45000 with 
    the last one to mimic one big track at LBA0 with the files at the 
    same LBA than the GD-ROM.
    """
    def __init__(self, wormfile1, wormfile2 =  None, *args, **kwargs):

        self._f1 = WormHoleFile(**wormfile1)

        self._f1.seek(0,2)
        self._f1_len = self._f1.tell()
        self._f1.seek(0,0)

        self._f2_len = 0
        if wormfile2:
            self._f2 = WormHoleFile(**wormfile2)

            self._f2.seek(0,2)
            self._f2_len = self._f2.tell()
            self._f2.seek(0,0)
        else:
            # So the rest of the code works for one or 2 files.
            self._f2 = StringIO('') 

        self.seek(0,0)


    def seek(self, a, b=0):
        if b == 0:
            self.MetaPointer = a
        if b == 1:
            self.MetaPointer += a
        if b == 2:
            self.MetaPointer = self._f1_len + self._f2_len - a

        if self.MetaPointer >= self._f1_len:
            self._f1.seek(0, 2)
            self._f2.seek(a - self._f1_len, 0)
        else:
            self._f1.seek(a, 0)
            self._f2.seek(0, 0)


    def read(self, length = None):
        if length == None:
            length = self._f1_len + self._f2_len - self.MetaPointer
        tmp = self.MetaPointer
        FutureOffset = self.MetaPointer + length
        if FutureOffset < self._f1_len: # Read inside file1
            data = self._f1.read(length)
        elif tmp > self._f1_len:        # Read inside file2
            data = self._f2.read(length)
        else:                           # Read end of file1 and start of file2
            data = self._f1.read(self._f1_len - tmp)
            data += self._f2.read(FutureOffset - self._f1_len)

        self.seek(FutureOffset) # It might be enough to just update 
                                # self.MetaPointer, but this is safer.
        return data


    def tell(self):
        return self.MetaPointer


    def __enter__(self):
        return self


    def __exit__(self, type=None, value=None, traceback=None):  
        # This is required to close files properly when using the with
        # statement. Which isn't required by ISO9660 anymore, but could
        # be useful for other uses so it stays!
        self._f1.__exit__()
        if self._f2_len:
            self._f2.__exit__()



def gdishrink(filename, odir=None, erase_bak=False, blank_check=True, verbose=True):
    """
    Function to shrink a GDI.

    *filename* should point to a valid gdi file with all tracks in the same folder.
    default output path *odir* in the same as the input one.
    """
    # 1- Managing filenames and input GDI infos
    absname = os.path.abspath(filename)
    basedir = os.path.dirname(absname)
    basename = os.path.basename(filename)
    isize = get_total_gdi_dumpsize(absname) # For saved-space comparison

    if odir is None:
        odir = basedir
    else:
        odir = os.path.abspath(odir)

    with GDIfile(absname) as gdi:
        itracks = gdi._gdi
        numtraks = len(itracks)
        if blank_check:
            first_blank_sector, first_file_sector = gdi.shrink_blank_check()
        else:
            first_blank_sector = gdi.get_last_toc_sector()
            first_file_sector = gdi.get_first_file_sector()
        sanity_check_file_A = gdi.get_file_by_record(gdi._sorted_records(crit='ex_loc')[0])
        
    # 2- We plan the new tracks, considering 3tracks or 5+tracks dumps
    otracks = deepcopy(itracks) # New tracks
    for t in otracks:
        t['filename'] = t['filename'].replace(basedir, odir)
    for i in [0,2,-1]:  # 1,3,last tracks: simpler than checking the number of tracks
        otracks[i]['mode'] = 2048
        otracks[i]['filename'] = otracks[i]['filename'].replace('.bin', '.iso')
    if numtraks==3:
        d = dict(
                filename = os.path.join(odir,'track04.iso'),
                lba = first_file_sector,
                mode = 2048,
                offset = 2048*(first_file_sector - first_blank_sector),    # Untested as of 2017-10-16
                tnum = 4,
                ttype = 'data'
                )
        otracks = otracks+[d]
    else:
        otracks[-1]['offset'] = 2048*(first_file_sector - first_blank_sector)
        otracks[-1]['lba'] = first_file_sector

    # 3- If we shrink in-folder, we backup everything in case it goes south
    if odir==basedir:
        itracks_bak = itracks[:3] 
        if numtraks > 3:
            itracks_bak = itracks_bak + [itracks[-1]]
        backup_files([absname]+[t['filename'] for t in itracks_bak])
        # Track list now point to backup files
        for t in itracks_bak:
            t['filename'] += '.bak'

    # 4- We copy the relevent data from input tracks to output tracks
    # *** THIS IS WHERE THE SHRINKING REALLY HAPPENS ***
    # TRACK03
    with ISO9660(itracks) as gdi, open(otracks[2]['filename'], 'wb') as ofile:
       gdifile = gdi._gdifile
       begin_offset = otracks[2]['lba']*2048
       length = first_blank_sector*2048 - begin_offset
       gdifile.seek(begin_offset)
       _copy_buffered(gdifile, ofile, length=length)
    # LAST TRACK
    with ISO9660(itracks) as gdi, open(otracks[-1]['filename'], 'wb') as ofile:
       gdifile = gdi._gdifile
       begin_offset = first_file_sector*2048
       gdifile.seek(begin_offset)
       _copy_buffered(gdifile, ofile, length=None) # Until the end
       
    # 5- Dummy and untouched tracks
    # Generating dummy files for track01 and track02
    # TRACK01
    with open(otracks[0]['filename'], 'wb') as ofile:
        ofile.write(getDummyDataTrack())
    # TRACK02
    with open(otracks[1]['filename'], 'wb') as ofile:
        ofile.write(getDummyAudioTrack())
    # Moving the HD-area audio tracks too if they exist
    if numtraks > 3:
        for iat, oat in zip(itracks, otracks)[3:-1]:    # Only the 2de session audio tracks
            if not os.path.isfile(oat['filename']):
                shutil.copy2(iat['filename'], oat['filename'])

    # 5- We dump the proper GDI file for the shrinked dump
    ofilename = os.path.join(odir, basename)
    with open(ofilename, 'wb') as f:
        f.write(gen_new_gdifile(otracks))
    # SANITY CHECK
    with GDIfile(ofilename) as gdi:
        sanity_check_file_B = gdi.get_file_by_record(gdi._sorted_records(crit='ex_loc')[0])
    if not sanity_check_file_A == sanity_check_file_B:
        raise AssertionError('Filesystem of the shrinked gdi is inconsistent. Cleaning skipped.')
    
    # 6- Post-shrinking cleaning
    if odir==basedir and erase_bak:
        erase_backup([absname+'.bak']+[t['filename'] for t in itracks_bak])

    # 7- Boasting about the compression... or lack thereof...
    osize = get_total_gdi_dumpsize(ofilename)
    if verbose:
        from math import log10
        a = (isize-osize)/1e6
        b = 100*(1-float(osize)/isize)
        c = 100-b
        # Aligning decimals with Math
        d = ' '*(3-int(log10(isize/1e6)))
        e = ' '*(3-int(log10(osize/1e6)))
        print('\nGDIshrink results on {}:'.format(filename))
        print('    Input:\t{}{:0.3f} MB\n    Output:\t{}{:.3f} MB'.format(d, isize/1e6, e, osize/1e6))
        print('Saved {:.3f} MB, or {:.2f}% at {:.2f}% compression ratio!\n'.format(a,b,c))
        
    return isize, osize # For testing, should be removed once shrinking works fine


def gen_new_gdifile(_tracks):
    tracks = deepcopy(_tracks)
    gdiline = '{tnum} {lba} {tracktype} {mode} {fname} {zero}\n'
    s = str(len(tracks))+'\n'
    for i,t in enumerate(tracks):
        pass
    for t in tracks:
        s += gdiline.format(
                tracktype= 4 if t['ttype']=='data' else 0, 
                fname=os.path.basename(t['filename']), 
                zero=0, 
                **t)
    return s


def get_total_gdi_dumpsize(filename):
    filename = os.path.abspath(filename)
    s=get_filesize(filename)
    with GDIfile(filename) as gdi:
        tracks=gdi._gdi
    for t in tracks:
        s += get_filesize(t['filename'])
    return s

def backup_files(files, verbose=False):
    """
    Backups *files*, appending '.bak' to the filenames.
    Never overwrites a previous backup.
    """
    if not isinstance(files, list):
        files = [files]
    for f in files:
        f = os.path.abspath(f)
        if not os.path.isfile(f+'.bak'):
            shutil.move(f, f+'.bak')
        else:
            if verbose: print("warning: backup file '{}.bak' already exists; backup skipped".format(f))


def restore_backup(bakfiles, verbose=False):
    """
    Restores files, removing the last 4 chars (that should be '.bak').
    Always overwrites target file if present.
    """
    if not isinstance(bakfiles, list):
        bakfiles = [bakfiles]
    for f in bakfiles:
        if not f[-4:] == '.bak':
            raise NameError("Backup file ({}) should end in '.bak' ".format(f))
        f = os.path.abspath(f)
        if os.path.isfile(f[:-4]):
            if verbose: print("warning: file '{} is being overwritten by backup".format(f[:-4]))
        shutil.move(f, f[:-4])


def erase_backup(bakfiles, verbose=False):
    """
    Erases the provided *bakfiles*
    """
    if not isinstance(bakfiles, list):
        bakfiles = [bakfiles]
    for f in bakfiles:
        os.remove(f)


def getDummyAudioTrack():
    return '\x00'*300*2352

def getDummyDataTrack():
    from zlib import decompress
    from base64 import b64decode
    # Compressed twice to make it smaller/prettier... really... I know it shouldn't...
    return decompress(decompress(b64decode('eNqruPX29um8yw4iDBc8b7qI7maKNUpyFV8iHO41ZelTn+DpFo6FShP5H27/sekf8xFdRkfvyI8BFu7bju8r/jvJTersCT5GBvzgRejlKW17DdaLz/7+8fmy2ctu2gXt33/v+Psu93e7o3eZfvn98IdeSc2ntb8qt1eK+r3v83Per/3r4eGLk5efj7sn8/Z8/5/QzXtW3E69O2WzTLl1oZHU0s+rT5vFXft8J+92aV7S1lNPq3Z2rV+92WKOj16SXM702Udzos59trOoC15i0nW9uXxnzNbTR39+XbB23auf60LP5RXveqWXLrP65avcvX2vY8zq/5R2m8gsV4rl6U76rqN59w73EvmWakmpt//9lh+2f3x5+j95oCcsYvbetix7XA9ivvlY/2LfemOwv9nfXP9/w3bOR6WaswblDKOAWHAg/7tkxo8DNwGBZqUF')))
    



def parse_gdi(filename, verbose=False):
    filename = os.path.realpath(filename)
    dirname = os.path.dirname(filename)

    with open(filename) as f: # if i.split() removes blank lines
        l = [i.split() for i in f.readlines() if i.split()]
    if not int(l[3][1]) == 45000:
        raise AssertionError('Invalid gdi file: track03 LBA should be 45000')

    nbt = int(l[0][0])

    gdi = [dict(filename=dirname + '/' + t[4], mode=int(t[3]), tnum=int(t[0]), lba=int(t[1]), 
                ttype='audio' if t[2]=='0' else 'data' if t[2]=='4' else 'unknown') 
                for t in l[1:]]

    gdi[2]['offset'] = 45000*2048
    gdi[2]['wormhole'] = [0, 45000*2048, 32*2048]

    if nbt > 3:
        gdi[nbt-1]['offset'] = 2048*(gdi[nbt-1]['lba'] - get_filesize(gdi[2]['filename'])/gdi[2]['mode'] - 45000)

    if verbose:
        print('\nParsed gdi file: {}'.format(os.path.basename(filename)))
        print('Base Directory:  {}'.format(dirname))
        print('Number of tracks:  {}'.format(nbt))
        for j in gdi:
            if j['ttype'] == 'data':
                if j['tnum']==1:
                    tlabel = 'PC DATA'
                elif j['tnum']==nbt:
                    tlabel = 'GAME DATA'
                else:
                    tlabel = 'TOC'
            else:
                tlabel = 'AUDIO'
            print('\nLOW-DENSITY:\n' if j['tnum']==1 else '\nHIGH-DENSITY:\n' if j['tnum']==3 else '')
            print('    {} track:'.format(tlabel))
            print('        Filename:  {}'.format(os.path.basename(j['filename'])))
            print('        LBA:       {} '.format(j['lba']))
            print('        Mode:      {} bytes/sector'.format(j['mode']))
            if j.has_key('offset'):
                print('        Offset:    {}'.format(j['offset']/2048))
            if j.has_key('wormhole'):
                print('        WormHole:  {}'.format([k/2048 for k in j['wormhole']]))
        print('')
 
    return gdi



def get_filesize(filename):
    with open(filename) as f:
        f.seek(0,2)
        return f.tell()


def UpdateLine(text):
    """
    Allows to print successive messages over the last line. Line is 
    force to be 80 chars long to avoid display issues.
    """
    import sys
    if len(text) > 80:
        text = text[:80]
    if text[-1] == '\r':
        text = text[:-1]
    text += ' '*(80-len(text))+'\r'
    sys.stdout.write(text)
    sys.stdout.flush()


def _copy_buffered(f1, f2, length = None, bufsize = 1*1024*1024, closeOut = True):
    """
    Copy istream f1 into ostream f2 in bufsize chunks
    """
    if length is None:  # By default it reads all the file
        tmp = f1.tell()
        f1.seek(0,2)
        length = f1.tell()
        f1.seek(tmp,0)
    f2.seek(0,0)

    for i in xrange(length/bufsize):
        f2.write(f1.read(bufsize))
    f2.write(f1.read(length % bufsize))

    #while length:
    #    chunk = min(length, bufsize)
    #    length = length - chunk
    #    data = f1.read(chunk)
    #    f2.write(data)

    if closeOut:
        f2.close()



def _printUsage(pname='gditools.py'):
    print('Usage: {} -i input_gdi [options]\n'.format(pname))
    print('  -h, --help             Display this help')
    print('  -l, --list             List all files in the filesystem and exit')
    print('  -o [outdir]            Output directory. Default: gdi folder')
    print('  -s [filename]          Create a sorttxt file with custom name')
    print('                           (It uses *data-folder* as prefix)')
    print('  -b [ipname]            Dump the ip.bin with custom name')
    print('  -e [filename]          Dump a single file from the filesystem')
    print('  --extract-all          Dump all the files in the *data-folder*')
    print('  --data-folder [name]   *data-folder* subfolder. Default: data')
    print(' '*27 + '(__volume_label__ --> Use ISO9660 volume label)')
    print('  --sort-spacer [num]    Sorttxt entries are sperated by num')
    print('  --silent               Minimal verbosity mode')
    print('  [no option]            Display gdi infos if not silent')
    print('\n')
    print('gditools.py by FamilyGuy, http://sourceforge.net/p/dcisotools/')
    print('    Licensed under GPLv3, see licences folder.')
    print('')
    print('iso9660.py  by Barney Gale, http://github.com/barneygale')
    print('    Licensed under a BSD-based license, see licences folder.')


def main(argv):
    progname = argv[0]
    argv=argv[1:]

    inputfile = ''
    outputpath = ''
    sorttxtfile = ''
    bootsectorfile = ''
    extract = ''
    silent = False
    datafolder = 'data'
    listFiles = False
    sort_spacer = 1
    try:
        opts, args = getopt.getopt(argv,"hli:o:s:b:e:",
                                   ['help','silent', 'list',
                                    'extract-all','data-folder=',
                                    'sort-spacer='])

    except getopt.GetoptError:
        _printUsage(progname)
        sys.exit(2)

    options = [o[0] for o in opts]

    if not '-i' in options:
        _printUsage(progname)
        sys.exit()

    if '-h' in options:
        _printUsage(progname)
        sys.exit()

    if '--help' in options:
        _printUsage(progname)
        sys.exit()

    if '-l' in options:
        listFiles = True

    if '--list' in options:
        listFiles = True

    for opt, arg in opts:
        if opt == '-i':
            inputfile = arg
        elif opt == '-o':
            outputpath = arg
        elif opt == '-s':
            sorttxtfile = arg
        elif opt == '-b':
            bootsectorfile = arg
        elif opt == '--silent':
            silent = True
        elif opt == '-e':
            extract = arg
        elif opt == '--extract-all':
            extract = '__all__'
        elif opt == '--data-folder':
            datafolder = arg
        elif opt == '--sort-spacer':
            sort_spacer = arg

    
    with GDIfile(inputfile, verbose = not silent) as gdi:
        if listFiles:
            print('Listing all files in the filesystem:\n')
            gdi.print_files()
            sys.exit()
         
        if outputpath:
            if outputpath[-1] == '/':
                outputpath = outputpath[:-1]

            gdi._dirname = os.path.abspath(outputpath)

            if not os.path.exists(gdi._dirname):
                os.makedirs(gdi._dirname)   
                if not silent: 
                    tmp_str = 'Created directory: {}'.format(gdi._dirname)
                    print(tmp_str + ' '*(80-len(tmp_str)))
        
        if datafolder == '__volume_label__':
            datafolder = gdi.get_volume_label()

        if sorttxtfile:
            gdi.dump_sorttxt(filename=sorttxtfile, prefix=datafolder, spacer = sort_spacer)

        if bootsectorfile:
            gdi.dump_bootsector(filename=bootsectorfile)

        if extract:
            if extract.lower() in ['__all__']:
                if not silent: print('\nDumping all files:')
                gdi.dump_all_files(target=datafolder)
            else:
                gdi.dump_file(extract, target=gdi._dirname)

        
if __name__ == '__main__':
    if len(sys.argv) > 1:
        main(sys.argv)
    else:
        _printUsage(sys.argv[0])

