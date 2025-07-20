using static System.IO.Hashing.Crc32;
using System.Text;
using System.Runtime.InteropServices;
using System.Net;
using System.Security.Cryptography;
using System.Drawing;

namespace bspPack;



class ZIP_EndOfCentralDirRecord
{
    public uint signature; //PK56
    public ushort numberOfThisDisk;
    public ushort numberOfTheDiskWithStartOfCentralDirectory;
    public ushort nCentralDirectoryEntries_ThisDisk;
    public ushort nCentralDirectoryEntries_Total;
    public uint centralDirectorySize;
    public uint startOfCentralDirOffset; //Relative to pakfile lump offset
    public ushort commentLength;
    public byte[] comment;

    //Helper
    public uint Size => (uint)(22 + commentLength);

    public ZIP_EndOfCentralDirRecord(BinaryReader reader)
    {
        signature = reader.ReadUInt32();
        numberOfThisDisk = reader.ReadUInt16();
        numberOfTheDiskWithStartOfCentralDirectory = reader.ReadUInt16();
        nCentralDirectoryEntries_ThisDisk = reader.ReadUInt16();
        nCentralDirectoryEntries_Total = reader.ReadUInt16();
        centralDirectorySize = reader.ReadUInt32();
        startOfCentralDirOffset = reader.ReadUInt32();
        commentLength = reader.ReadUInt16();
        comment = reader.ReadBytes(commentLength);
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(signature);
        writer.Write(numberOfThisDisk);
        writer.Write(numberOfTheDiskWithStartOfCentralDirectory);
        writer.Write(nCentralDirectoryEntries_ThisDisk);
        writer.Write(nCentralDirectoryEntries_Total);
        writer.Write(centralDirectorySize);
        writer.Write(startOfCentralDirOffset);
        writer.Write(commentLength);
        writer.Write(comment);
    }
    // public override string ToString()
    // {
    //     string s = "";
    //     s += $"signature: {signature}\n";
    //     s += $"numberOfThisDisk: {numberOfThisDisk}\n";
    //     s += $"numberOfTheDiskWithStartOfCentralDirectory: {numberOfTheDiskWithStartOfCentralDirectory}\n";
    //     s += $"nCentralDirectoryEntries_ThisDisk: {nCentralDirectoryEntries_ThisDisk}\n";
    //     s += $"nCentralDirectoryEntries_Total: {nCentralDirectoryEntries_Total}\n";
    //     s += $"centralDirectorySize: {centralDirectorySize}\n";
    //     s += $"startOfCentralDirOffset: {startOfCentralDirOffset}\n";
    //     s += $"commentLength: {commentLength}\n";
    //     s += "comment: " + Encoding.ASCII.GetString(comment);
    //     return s;
    // }
}

class ZIP_FileHeader
{
    public uint signature; //PK12 
    public ushort versionMadeBy;
    public ushort versionNeededToExtract;
    public ushort flags;
    public ushort compressionMethod;
    public ushort lastModifiedTime;
    public ushort lastModifiedDate;
    public uint crc32;
    public uint compressedSize;
    public uint uncompressedSize;
    public ushort fileNameLength;
    public ushort extraFieldLength;
    public ushort fileCommentLength;
    public ushort diskNumberStart;
    public ushort internalFileAttribs;
    public uint externalFileAttribs;
    public uint relativeOffsetOfLocalHeader;
    public byte[] fileName;
    public byte[] extraField;
    public byte[] fileComment;

    //Helper
    public uint Size => (uint)(46 + fileNameLength + extraFieldLength + fileCommentLength);

    public ZIP_FileHeader(BinaryReader reader)
    {
        signature = reader.ReadUInt32();
        versionMadeBy = reader.ReadUInt16();
        versionNeededToExtract = reader.ReadUInt16();
        flags = reader.ReadUInt16();
        compressionMethod = reader.ReadUInt16();
        lastModifiedTime = reader.ReadUInt16();
        lastModifiedDate = reader.ReadUInt16();
        crc32 = reader.ReadUInt32();
        compressedSize = reader.ReadUInt32();
        uncompressedSize = reader.ReadUInt32();
        fileNameLength = reader.ReadUInt16();
        extraFieldLength = reader.ReadUInt16();
        fileCommentLength = reader.ReadUInt16();
        diskNumberStart = reader.ReadUInt16();
        internalFileAttribs = reader.ReadUInt16();
        externalFileAttribs = reader.ReadUInt32();
        relativeOffsetOfLocalHeader = reader.ReadUInt32();
        fileName = reader.ReadBytes(fileNameLength);
        extraField = reader.ReadBytes(extraFieldLength);
        fileComment = reader.ReadBytes(fileCommentLength);
    }

    public ZIP_FileHeader(ZIP_LocalFileHeader localHeader, uint offset)
    {
        signature = 33639248; //PK12
        versionMadeBy = 20; //Windows //TODO
        versionNeededToExtract = localHeader.versionNeededToExtract;
        flags = 0;
        compressionMethod = localHeader.compressionMethod;
        lastModifiedTime = 0; //Ignore
        lastModifiedDate = 0; //Ignore
        crc32 = localHeader.crc32;
        compressedSize = localHeader.compressedSize;
        uncompressedSize = localHeader.uncompressedSize;
        fileNameLength = localHeader.fileNameLength;
        extraFieldLength = 0; //Ignore
        fileCommentLength = 0; //Ignore
        diskNumberStart = 0; //Ignore
        internalFileAttribs = 0; //Ignore
        externalFileAttribs = 0; //Ignore
        relativeOffsetOfLocalHeader = offset;
        fileName = localHeader.fileName;
        extraField = []; //Ignore
        fileComment = []; //Ignore
    }

    // public override string ToString()
    // {
    //     string s = "";
    //     s += $"signature: {signature}\n";
    //     s += $"versionMadeBy: {versionMadeBy}\n";
    //     s += $"versionNeededToExtract: {versionNeededToExtract}\n";
    //     s += $"flags: {flags}\n";
    //     s += $"compressionMethod: {compressionMethod}\n";
    //     s += $"lastModifiedTime: {lastModifiedTime}\n";
    //     s += $"lastModifiedDate: {lastModifiedDate}\n";
    //     s += $"crc32: {crc32}\n";
    //     s += $"compressedSize: {compressedSize}\n";
    //     s += $"uncompressedSize: {uncompressedSize}\n";
    //     s += $"fileNameLength: {fileNameLength}\n";
    //     s += $"extraFieldLength: {extraFieldLength}\n";
    //     s += $"fileCommentLength: {fileCommentLength}\n";
    //     s += $"diskNumberStart: {diskNumberStart}\n";
    //     s += $"internalFileAttribs: {internalFileAttribs}\n";
    //     s += $"externalFileAttribs: {externalFileAttribs}\n";
    //     s += $"relativeOffsetOfLocalHeader: {relativeOffsetOfLocalHeader}\n";
    //     s += "filename: " + Encoding.ASCII.GetString(fileName) + "\n";
    //     s += "extra field: " + Encoding.ASCII.GetString(extraField) + "\n";
    //     s += "file comment: " + Encoding.ASCII.GetString(fileComment) + "\n";
    //     return s;
    // }

    public void Write(BinaryWriter writer)
    {
        writer.Write(signature);
        writer.Write(versionMadeBy);
        writer.Write(versionNeededToExtract);
        writer.Write(flags);
        writer.Write(compressionMethod);
        writer.Write(lastModifiedTime);
        writer.Write(lastModifiedDate);
        writer.Write(crc32);
        writer.Write(compressedSize);
        writer.Write(uncompressedSize);
        writer.Write(fileNameLength);
        writer.Write(extraFieldLength);
        writer.Write(fileCommentLength);
        writer.Write(diskNumberStart);
        writer.Write(internalFileAttribs);
        writer.Write(externalFileAttribs);
        writer.Write(relativeOffsetOfLocalHeader);
        writer.Write(fileName!);

        if (extraFieldLength > 0 && extraField != null)
            writer.Write(extraField);
        if (fileCommentLength > 0 && fileComment != null)
            writer.Write(fileComment);
    }
}
class ZIP_LocalFileHeader
{
    public uint signature = 67324752; //PK34 
    public ushort versionNeededToExtract;
    public ushort flags;
    public ushort compressionMethod;
    public ushort lastModifiedTime;
    public ushort lastModifiedDate;
    public uint crc32;
    public uint compressedSize;
    public uint uncompressedSize;
    public ushort fileNameLength;
    public ushort extraFieldLength;
    public byte[] fileName;
    public byte[] extraField = [];

    //Helper
    public uint Size => (uint)(30 + fileNameLength + extraFieldLength);

    public ZIP_LocalFileHeader(BinaryReader reader)
    {
        signature = reader.ReadUInt32();
        versionNeededToExtract = reader.ReadUInt16();
        flags = reader.ReadUInt16();
        compressionMethod = reader.ReadUInt16();
        lastModifiedTime = reader.ReadUInt16();
        lastModifiedDate = reader.ReadUInt16();
        crc32 = reader.ReadUInt32();
        compressedSize = reader.ReadUInt32();
        uncompressedSize = reader.ReadUInt32();
        fileNameLength = reader.ReadUInt16();
        extraFieldLength = reader.ReadUInt16();
        fileName = reader.ReadBytes(fileNameLength);
        if (extraFieldLength > 0) extraField = reader.ReadBytes(extraFieldLength);
    }

    public ZIP_LocalFileHeader(ReadOnlySpan<byte> fileData, string internalPath)
    {
        signature = 67324752; //PK34
        versionNeededToExtract = 10;
        flags = 0; //Ignore
        compressionMethod = 0;
        lastModifiedTime = 0; //Ignore
        lastModifiedDate = 0; //Ignore
        crc32 = HashToUInt32(fileData);
        compressedSize = uncompressedSize = (uint)fileData.Length;
        fileNameLength = (ushort)internalPath.Length;
        extraFieldLength = 0; //Ignore
        fileName = Encoding.ASCII.GetBytes(internalPath);
        extraField = [];
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(signature);
        writer.Write(versionNeededToExtract);
        writer.Write(flags);
        writer.Write(compressionMethod);
        writer.Write(lastModifiedTime);
        writer.Write(lastModifiedDate);
        writer.Write(crc32);
        writer.Write(compressedSize);
        writer.Write(uncompressedSize);
        writer.Write(fileNameLength);
        writer.Write(extraFieldLength);
        if (fileNameLength != 0) writer.Write(fileName);
        if (extraFieldLength != 0) writer.Write(extraField);
    }
};

class LUMP
{
    public uint fileofs;
    public uint filelen;
    public uint version;
    public byte[] fourCC = new byte[4];

    //Helper
    public const int Size = 16;

    public LUMP(BinaryReader reader)
    {
        fileofs = reader.ReadUInt32();
        filelen = reader.ReadUInt32();
        version = reader.ReadUInt32();
        for (int i = 0; i < 4; i++)
            fourCC[i] = reader.ReadByte();
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(fileofs);
        writer.Write(filelen);
        writer.Write(version);
        writer.Write(fourCC);
    }

    // public override string ToString()
    // {
    //     string s = "";
    //     s += $"file offset: {fileofs}\n";
    //     s += $"file length: {filelen}\n";
    //     s += $"version: {version}\n";
    //     s += $"fourCC: {Encoding.ASCII.GetString(fourCC)}\n";

    //     return s;
    // }
}

class LZMA_HEADER
{
    public uint id = ('A' << 24) | ('M' << 16) | ('Z' << 8) | 'L';
    public uint actualSize;
    public uint lzmaSize = 0;

    //5 properties bytes can be written with PropertiesByte followed by DICT_SIZE
    public const int LC = 3;
    public const int LP = 0;
    public const int PB = 2;
    private const byte PROPERTIES_BYTE = (PB * 5 + LP) * 9 + LC;
    public const int DICT_SIZE = 1024 * 1024;

    //Helper
    public const uint Size = 17;


    public LZMA_HEADER(uint _actualSize, uint _lzmaSize)
    {
        actualSize = _actualSize;
        lzmaSize = _lzmaSize;
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(id);
        writer.Write(actualSize);
        writer.Write(lzmaSize);
        writer.Write(PROPERTIES_BYTE);
        writer.Write(DICT_SIZE);
    }
}

class DGAMELUMP
{
    public uint id;
    public ushort flags;
    public ushort version;
    public uint fileofs;
    public uint filelen;

    //Helper
    public const int Size = 16;

    public DGAMELUMP(BinaryReader reader)
    {
        id = reader.ReadUInt32();
        flags = reader.ReadUInt16();
        version = reader.ReadUInt16();
        fileofs = reader.ReadUInt32();
        filelen = reader.ReadUInt32();
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(id);
        writer.Write(flags);
        writer.Write(version);
        writer.Write(fileofs);
        writer.Write(filelen);
    }
}
