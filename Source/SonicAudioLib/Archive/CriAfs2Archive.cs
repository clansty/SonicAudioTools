﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using SonicAudioLib.IO;
using SonicAudioLib.Module;

namespace SonicAudioLib.Archive
{
    public class CriAfs2Entry : EntryBase
    {
        public ushort Id { get; set; }
    }

    public class CriAfs2Archive : ArchiveBase<CriAfs2Entry>
    {
        public uint Align { get; set; }
        public uint IdFieldLength { get; set; }
        public uint PositionFieldLength { get; set; }

        public override void Read(Stream source)
        {
            if (EndianStream.ReadCString(source, 4) != "AFS2")
            {
                throw new InvalidDataException("'AFS2' signature could not be found.");
            }

            uint information = EndianStream.ReadUInt32(source);

            uint type = information & 0xFF;
            if (type != 1)
            {
                throw new InvalidDataException($"Unknown AFS2 type ({type}). Please report this error with the file(s).");
            }

            IdFieldLength = (information >> 16) & 0xFF;
            PositionFieldLength = (information >> 8) & 0xFF;

            ushort entryCount = (ushort)EndianStream.ReadUInt32(source);
            Align = EndianStream.ReadUInt32(source);

            CriAfs2Entry previousEntry = null;
            for (uint i = 0; i < entryCount; i++)
            {
                CriAfs2Entry afs2Entry = new CriAfs2Entry();

                long idPosition = 16 + (i * IdFieldLength);
                source.Seek(idPosition, SeekOrigin.Begin);

                switch (IdFieldLength)
                {
                    case 2:
                        afs2Entry.Id = EndianStream.ReadUInt16(source);
                        break;

                    default:
                        throw new InvalidDataException($"Unknown IdFieldLength ({IdFieldLength}). Please report this error with the file(s).");
                }

                long positionPosition = 16 + (entryCount * IdFieldLength) + (i * PositionFieldLength);
                source.Seek(positionPosition, SeekOrigin.Begin);

                switch (PositionFieldLength)
                {
                    case 2:
                        afs2Entry.Position = EndianStream.ReadUInt16(source);
                        break;

                    case 4:
                        afs2Entry.Position = EndianStream.ReadUInt32(source);
                        break;

                    default:
                        throw new InvalidDataException($"Unknown PositionFieldLength ({PositionFieldLength}). Please report this error with the file(s).");
                }

                if (previousEntry != null)
                {
                    previousEntry.Length = afs2Entry.Position - previousEntry.Position;
                }

                afs2Entry.Position = Helpers.Align(afs2Entry.Position, Align);

                if (i == entryCount - 1)
                {
                    switch (PositionFieldLength)
                    {
                        case 2:
                            afs2Entry.Length = EndianStream.ReadUInt16(source) - afs2Entry.Position;
                            break;

                        case 4:
                            afs2Entry.Length = EndianStream.ReadUInt32(source) - afs2Entry.Position;
                            break;
                    }
                }

                entries.Add(afs2Entry);
                previousEntry = afs2Entry;
            }
        }

        public override void Write(Stream destination)
        {
            uint headerLength = (uint)(16 + (entries.Count * IdFieldLength) + (entries.Count * PositionFieldLength) + PositionFieldLength);

            EndianStream.WriteCString(destination, "AFS2", 4);
            EndianStream.WriteUInt32(destination, 1 | (IdFieldLength << 16) | (PositionFieldLength << 8));
            EndianStream.WriteUInt32(destination, (ushort)entries.Count);
            EndianStream.WriteUInt32(destination, Align);
            
            VldPool vldPool = new VldPool(Align, headerLength);

            var orderedEntries = entries.OrderBy(entry => entry.Id);
            foreach (CriAfs2Entry afs2Entry in orderedEntries)
            {
                switch (IdFieldLength)
                {
                    case 2:
                        EndianStream.WriteUInt16(destination, (ushort)afs2Entry.Id);
                        break;

                    default:
                        throw new NotImplementedException($"Unimplemented IdFieldLength ({IdFieldLength}). Implemented length: (2)");
                }
            }

            foreach (CriAfs2Entry afs2Entry in orderedEntries)
            {
                uint entryPosition = (uint)vldPool.Length;
                vldPool.Put(afs2Entry.FilePath);

                switch (PositionFieldLength)
                {
                    case 2:
                        EndianStream.WriteUInt16(destination, (ushort)entryPosition);
                        break;

                    case 4:
                        EndianStream.WriteUInt32(destination, entryPosition);
                        break;

                    default:
                        throw new InvalidDataException($"Unimplemented PositionFieldLength ({PositionFieldLength}). Implemented lengths: (2, 4)");
                }

                afs2Entry.Position = entryPosition;
            }

            EndianStream.WriteUInt32(destination, (uint)vldPool.Length);

            vldPool.Write(destination);
            vldPool.Clear();
        }

        public CriAfs2Entry GetById(uint cueIndex)
        {
            return entries.FirstOrDefault(e => (e.Id == cueIndex));
        }
        
        public CriAfs2Archive()
        {
            Align = 32;
            IdFieldLength = 2;
            PositionFieldLength = 4;
        }
    }
}
