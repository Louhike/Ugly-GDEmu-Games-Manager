using GDEmuSdCardManager.DTO;
using GDEmuSdCardManager.DTO.CDI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GDEmuSdCardManager.BLL.ImageReaders
{
    public class CdiReader : BaseImageReader
    {
        public BaseGame ExtractGameData(string imagePath)
        {
            string folderPath = new FileInfo(imagePath).DirectoryName;
            var game = new BaseGame
            {
                FullPath = folderPath,
                Path = folderPath.Split(Path.DirectorySeparatorChar).Last(),
                Size = FileManager.GetDirectorySize(folderPath),
                FormattedSize = FileManager.GetDirectoryFormattedSize(folderPath)
            };

            using (var fs = File.OpenRead(imagePath))
            {
                game.CdiInfo = GetCdiFromFile(fs);
                RetrieveTrack3DataFromCdiStream(game, fs);
            }

            return game;
        }

        private void RetrieveTrack3DataFromCdiStream(BaseGame game, Stream fs)
        {
            var track3 = game.CdiInfo.Sessions[1].Tracks[0];

            bool isRawMode = track3.SectorSize == 2352; // 2352/RAW mode or 2048
            long startingPosition = track3.Position + (track3.PregapLength * (long)track3.SectorSize);
            fs.Seek(startingPosition, SeekOrigin.Begin);

            byte[] emptyBuffer = new byte[1];
            do
            {
                fs.Read(emptyBuffer, 0, 1);
            } while (emptyBuffer[0] == 0);

            fs.Seek(fs.Position - 1, SeekOrigin.Begin);

            if (isRawMode)
            {
                // We ignore the first line
                byte[] dummyBuffer = new byte[16];
                fs.Read(dummyBuffer, 0, 16);
            }

            ReadGameInfoFromBinaryData(game, fs);
        }

        /// <summary>
        /// Read a CDI file.
        /// Most of the code is based on cdirip (https://sourceforge.net/projects/cdimagetools/).
        /// </summary>
        /// <param name="cdiStream"></param>
        /// <returns></returns>
        private Cdi GetCdiFromFile(Stream cdiStream)
        {
            var cdi = new Cdi();

            long length = cdiStream.Seek(0L, SeekOrigin.End);
            cdiStream.Seek(length - 8, SeekOrigin.Begin);

            long globalTrackPosition = 0;
            byte[] buffer1 = new byte[1];
            byte[] buffer2 = new byte[2];
            byte[] buffer4 = new byte[4];

            cdiStream.Read(buffer4, 0, 4);
            uint cdiVersion = BitConverter.ToUInt32(buffer4);

            cdiStream.Read(buffer4, 0, 4);
            uint headerOffset = BitConverter.ToUInt32(buffer4);

            cdiStream.Seek(length - headerOffset, SeekOrigin.Begin);

            cdiStream.Read(buffer2, 0, 2);
            ushort numberOfSessions = BitConverter.ToUInt16(buffer2);

            cdi.Sessions = new List<CdiSession>();
            for (int i = 0; i < numberOfSessions; i++)
            {
                var session = new CdiSession();
                session.Tracks = new List<CdiTrack>();

                cdiStream.Read(buffer2, 0, 2);
                ushort numberOfTracks = BitConverter.ToUInt16(buffer2);
                for (int j = 0; j < numberOfTracks; j++)
                {
                    var track = new CdiTrack();
                    track.Number = j + 1;

                    byte[] trackStartMark = { 0, 0, 0x01, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF };
                    byte[] trackStartMarkBuffer = new byte[10];

                    cdiStream.Read(buffer4, 0, 4);
                    if (BitConverter.ToUInt32(buffer4) != 0)
                    {
                        cdiStream.Seek(8, SeekOrigin.Current);
                    }

                    for (int k = 0; k < 2; k++)
                    {
                        cdiStream.Read(trackStartMarkBuffer, 0, 10);
                        if (!trackStartMarkBuffer.SequenceEqual(trackStartMark))
                        {
                            throw new Exception("Bad CDI format. Incorrect track start mark.");
                        }
                    }

                    cdiStream.Seek(4, SeekOrigin.Current);
                    cdiStream.Read(buffer1, 0, 1);
                    track.FilenameLength = buffer1[0];
                    cdiStream.Seek(track.FilenameLength, SeekOrigin.Current);

                    cdiStream.Seek(11, SeekOrigin.Current);
                    cdiStream.Seek(4, SeekOrigin.Current);
                    cdiStream.Seek(4, SeekOrigin.Current);

                    cdiStream.Read(buffer4, 0, 4);
                    if (BitConverter.ToUInt32(buffer4) == 0x80000000)
                    {
                        cdiStream.Seek(8, SeekOrigin.Current);
                    }

                    cdiStream.Seek(2, SeekOrigin.Current);

                    cdiStream.Read(buffer4, 0, 4);
                    track.PregapLength = BitConverter.ToUInt32(buffer4);

                    cdiStream.Read(buffer4, 0, 4);
                    track.Length = BitConverter.ToUInt32(buffer4);

                    cdiStream.Seek(6, SeekOrigin.Current);

                    cdiStream.Read(buffer4, 0, 4);
                    track.Mode = BitConverter.ToUInt32(buffer4);

                    cdiStream.Seek(12, SeekOrigin.Current);

                    cdiStream.Read(buffer4, 0, 4);
                    track.StartLba = BitConverter.ToUInt32(buffer4);

                    cdiStream.Read(buffer4, 0, 4);
                    track.TotalLength = BitConverter.ToUInt32(buffer4);

                    cdiStream.Seek(16, SeekOrigin.Current);

                    cdiStream.Read(buffer4, 0, 4);
                    track.SectorSizeValue = BitConverter.ToUInt32(buffer4);

                    switch (track.SectorSizeValue)
                    {
                        case 0: track.SectorSize = 2048; break;
                        case 1: track.SectorSize = 2336; break;
                        case 2: track.SectorSize = 2352; break;
                        default:
                            throw new Exception($"Unexpected SectorSizeValue in CDI ({track.SectorSizeValue}).");
                    }

                    if (track.Mode > 2)
                    {
                        throw new Exception($"Unmanaged track mode ({track.Mode}).");
                    }

                    cdiStream.Seek(29, SeekOrigin.Current);

                    if (cdiVersion != Cdi.CdiVersion2)
                    {
                        cdiStream.Seek(5, SeekOrigin.Current);
                        cdiStream.Read(buffer4, 0, 4);
                        if (BitConverter.ToUInt32(buffer4) == 0xffffffff)
                        {
                            cdiStream.Seek(78, SeekOrigin.Current);
                        }
                    }

                    session.Tracks.Add(track);

                    var position = cdiStream.Position;

                    if (track.TotalLength < track.PregapLength + track.Length)
                    {
                        cdiStream.Seek(globalTrackPosition, SeekOrigin.Begin);
                        cdiStream.Seek(track.TotalLength, SeekOrigin.Current);
                        track.Position = cdiStream.Position;
                        globalTrackPosition = cdiStream.Position;
                    }
                    else
                    {
                        cdiStream.Seek(globalTrackPosition, SeekOrigin.Begin);
                        track.Position = cdiStream.Position;
                        cdiStream.Seek(track.TotalLength * (long)track.SectorSize, SeekOrigin.Current);
                        globalTrackPosition = cdiStream.Position;
                    }

                    cdiStream.Seek(position, SeekOrigin.Begin);
                }

                cdi.Sessions.Add(session);

                cdiStream.Seek(4, SeekOrigin.Current);
                cdiStream.Seek(8, SeekOrigin.Current);

                if (cdiVersion != Cdi.CdiVersion2)
                {
                    cdiStream.Seek(1, SeekOrigin.Current);
                }
            }

            if (cdi.Sessions.Count != 2)
            {
                throw new Exception("Cannot manage CDI with something else than two sessions.");
            }

            if (cdi.Sessions[1].Tracks.Count != 1)
            {
                throw new Exception("Cannot manage CDI with the second session not having one track.");
            }

            return cdi;
        }
    }
}