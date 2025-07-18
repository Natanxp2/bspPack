using System.Reflection.Metadata;
using System.Text;
using static System.IO.Hashing.Crc32;

namespace bspPack;

public class LumpManager
{
    readonly static int LZMA_ID = ('A' << 24) | ('M' << 16) | ('Z' << 8) | ('L');
    readonly static List<ZIP_FileHeader> CentralDir = [];
    readonly static HashSet<string> PackedAssets = [];
    static ZIP_EndOfCentralDirRecord? EOCD;

    public static void PackBSP(IDictionary<string, string> pakfile)
    {
        int PAKFILE_LUMP_OFFSET = 8 + (40 * 16);
        string tempBsp = Config.BSPFile[..^4] + "_backup.bsp";
        File.Copy(Config.BSPFile, tempBsp, overwrite: true);

        using (var fs = new FileStream(tempBsp, FileMode.Open, FileAccess.ReadWrite))
        using (var reader = new BinaryReader(fs))
        using (var writer = new BinaryWriter(fs))
        {
            //Check the ID of first lump header for compression signature
            fs.Seek(8, SeekOrigin.Begin);
            if (reader.ReadInt32() == LZMA_ID)
            {
                Message.Error("File is compressed. Exiting");
                Environment.Exit(1);
            }

            //Get pakfile Offset
            fs.Seek(PAKFILE_LUMP_OFFSET, SeekOrigin.Begin);
            uint pakfileOffset = reader.ReadUInt32();

            //Store EOCD and centralDir array before packing
            long eocdOffset = FindEOCDOffset(fs);

            fs.Seek(eocdOffset, SeekOrigin.Begin);
            EOCD = new ZIP_EndOfCentralDirRecord(reader);

            fs.Seek(pakfileOffset + EOCD.startOfCentralDirOffset, SeekOrigin.Begin);
            for (int i = 0; i < EOCD.nCentralDirectoryEntries_Total; i++)
            {
                var centralDirHeader = new ZIP_FileHeader(reader);
                PackedAssets.Add(Encoding.ASCII.GetString(centralDirHeader.fileName!));
                CentralDir.Add(centralDirHeader);
            }

            //Strip centralDir and EOCD to prepare for appending
            fs.SetLength(pakfileOffset + EOCD.startOfCentralDirOffset);
            fs.Seek(0, SeekOrigin.End);


            uint pakfileLength = (uint)fs.Position - pakfileOffset;

            //Append new localHeaders and fileData
            foreach (var filepath in pakfile) // .Key = internal | .Value = external
            {
                if (PackedAssets.Contains(filepath.Key)) continue;

                ReadOnlySpan<byte> fileData = File.ReadAllBytes(filepath.Value);
                var localheader = new ZIP_LocalFileHeader(fileData, filepath.Key);
                CentralDir.Add(new ZIP_FileHeader(localheader, (uint)fs.Position - pakfileOffset));

                localheader.Write(writer);
                writer.Write(fileData);

                pakfileLength += localheader.Size + (uint)fileData.Length;
            }

            //Patch part of EOCD
            EOCD.startOfCentralDirOffset = (uint)fs.Position - pakfileOffset;
            EOCD.nCentralDirectoryEntries_ThisDisk = (ushort)CentralDir.Count;
            EOCD.nCentralDirectoryEntries_Total = (ushort)CentralDir.Count;

            //Append new CentralDir
            uint centralDirSize = 0;
            foreach (var header in CentralDir)
            {
                centralDirSize += header.Size;
                header.Write(writer);
            }

            pakfileLength += centralDirSize;

            //Finish patching EOCD and append
            EOCD.centralDirectorySize = centralDirSize;
            EOCD.Write(writer);

            //Pad to 4 bytes
            int padding = (int)(4 - (fs.Position % 4)) % 4;
            if (padding > 0)
                writer.Write(new byte[padding]);

            //Patch pakfileLenght in lump 40 header
            pakfileLength += EOCD.Size;
            fs.Seek(PAKFILE_LUMP_OFFSET + 4, SeekOrigin.Begin);
            writer.Write(pakfileLength);
        }
    }

    static long FindEOCDOffset(FileStream fs)
    {
        const int maxSearch = 0xFFFF + 22;
        byte[] buffer = new byte[maxSearch];
        fs.Seek(-Math.Min(maxSearch, fs.Length), SeekOrigin.End);
        fs.ReadExactly(buffer);

        for (int i = buffer.Length - 22; i >= 0; i--)
        {
            if (buffer[i] == 0x50 && buffer[i + 1] == 0x4B && buffer[i + 2] == 0x05 && buffer[i + 3] == 0x06)
                return fs.Length - buffer.Length + i;
        }

        throw new Exception("EOCD not found");
    }
}
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
        compressedSize = localHeader.compressedSize; // TODO
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
    public byte[] extraField;

    //Helper
    public uint Size => (uint)(46 + fileNameLength + extraFieldLength);

    public ZIP_LocalFileHeader(ReadOnlySpan<byte> fileData, string internalPath)
    {
        signature = 67324752; //PK34
        versionNeededToExtract = 10;
        flags = 0; //Ignore
        compressionMethod = 0; //TODO
        lastModifiedTime = 0; //Ignore
        lastModifiedDate = 0; //Ignore
        crc32 = HashToUInt32(fileData);
        compressedSize = (uint)fileData.Length; //TODO
        uncompressedSize = (uint)fileData.Length;
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

