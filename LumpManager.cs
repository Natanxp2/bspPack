using System.Text;

namespace bspPack;

public partial class LumpManager
{
    const int BSP_ID = ('P' << 24) + ('S' << 16) + ('B' << 8) + 'V';
    const byte GAMELUMP_INDEX = 35;
    const byte PAKFILE_INDEX = 40;
    const byte VERSION_NEEDED_TO_EXTRACT = 63;
    const byte COMPRESSION_METHOD = 14;
    const int PAKFILE_LUMP_OFFSET = 8 + (PAKFILE_INDEX * LUMP.Size);

    readonly static List<ZIP_FileHeader> CentralDir = [];
    readonly static HashSet<string> PackedAssets = [];
    static ZIP_EndOfCentralDirRecord? EOCD;

    public static void PackBSP(IDictionary<string, string> pakfile, bool compress)
    {
        using var fs = new FileStream(Config.BSPFile, FileMode.Open, FileAccess.ReadWrite);
        using var reader = new BinaryReader(fs);

        GetEOCD(fs, reader);

        fs.Seek(PAKFILE_LUMP_OFFSET, SeekOrigin.Begin);
        uint pakfileOffset = reader.ReadUInt32();

        fs.Seek(pakfileOffset + EOCD!.startOfCentralDirOffset, SeekOrigin.Begin);
        for (int i = 0; i < EOCD.nCentralDirectoryEntries_Total; i++)
        {
            var centralDirHeader = new ZIP_FileHeader(reader);
            PackedAssets.Add(Encoding.ASCII.GetString(centralDirHeader.fileName!));
            CentralDir.Add(centralDirHeader);
        }

        if (compress)
        {
            var (outputStream, outputPakfileOffset) = CompressBSP(fs, pakfileOffset);
            AppendPakfile(pakfile, (uint)outputPakfileOffset, outputStream, compress);
        }
        else
            AppendPakfile(pakfile, pakfileOffset, fs, compress: false);

    }
    public static void AppendPakfile(IDictionary<string, string> pakfile, uint pakfileOffset, FileStream fs, bool compress)
    {
        using var writer = new BinaryWriter(fs);

        //Strip centralDir and EOCD to prepare for appending
        if (!compress)
        {
            fs.SetLength(pakfileOffset + EOCD!.startOfCentralDirOffset);
            fs.Seek(0, SeekOrigin.End);
        }

        uint pakfileLength = (uint)fs.Position - pakfileOffset;

        //Append new localHeaders and fileData
        foreach (var filepath in pakfile) // .Key = internal | .Value = external
        {
            if (PackedAssets.Contains(filepath.Key)) continue;

            uint localHeaderOffset = (uint)(fs.Position - pakfileOffset);
            byte[] fileData = File.ReadAllBytes(filepath.Value);
            var localheader = new ZIP_LocalFileHeader(fileData, filepath.Key);

            localheader.Write(writer);
            if (compress)
            {
                using var fileStream = new MemoryStream(fileData);
                localheader.compressedSize = CompressLZMA(fileStream, fs, 0, IsPakfile: true);
                localheader.versionNeededToExtract = VERSION_NEEDED_TO_EXTRACT;
                localheader.compressionMethod = COMPRESSION_METHOD;
            }
            else
                writer.Write(fileData);

            CentralDir.Add(new ZIP_FileHeader(localheader, localHeaderOffset));
            pakfileLength += localheader.Size + localheader.compressedSize;
        }

        //Patch part of EOCD
        EOCD!.startOfCentralDirOffset = (uint)fs.Position - pakfileOffset;
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
        PadTo4Bytes(fs, writer);

        //Patch offset and length in lump 40 header
        pakfileLength += EOCD.Size;
        fs.Seek(PAKFILE_LUMP_OFFSET, SeekOrigin.Begin);
        writer.Write(pakfileOffset);
        writer.Write(pakfileLength);
    }

    public static (FileStream outputStream, long outputPakfileOffset) CompressBSP(FileStream inputStream, uint inputPakfileOffset)
    {
        string tempBsp = Config.BSPFile[..^4] + $"_{Guid.NewGuid()}.bsp";
        LUMP[] lumps = new LUMP[64];

        FileStream outputStream = File.Create(tempBsp);

        using BinaryReader reader = new(inputStream);
        using BinaryWriter writer = new(outputStream, Encoding.UTF8, leaveOpen: true);

        inputStream.Seek(4, SeekOrigin.Begin);
        uint bspVersion = reader.ReadUInt32();

        for (int i = 0; i < 64; i++)
            lumps[i] = new LUMP(reader);

        uint mapRevision = reader.ReadUInt32();

        //Write BSP header with dummy lump headers
        writer.Write(BSP_ID);
        writer.Write(bspVersion);
        outputStream.Seek(LUMP.Size * 64, SeekOrigin.Current);
        writer.Write(mapRevision);

        //Write compressed data with LZMA headers
        for (int j = 0; j < lumps.Length; j++)
        {
            if (lumps[j].filelen == 0)
            {
                lumps[j].fileofs = 0;
                continue;
            }
            if (j == GAMELUMP_INDEX || j == PAKFILE_INDEX) continue;

            PadTo4Bytes(outputStream, writer);

            inputStream.Seek(lumps[j].fileofs, SeekOrigin.Begin);
            lumps[j].fileofs = (uint)outputStream.Position;

            outputStream.Seek(LZMA_HEADER.Size, SeekOrigin.Current);
            uint lzmaSize = CompressLZMA(inputStream, outputStream, lumps[j].filelen);

            outputStream.Seek(lumps[j].fileofs, SeekOrigin.Begin);
            new LZMA_HEADER(lumps[j].filelen, lzmaSize).Write(writer);
            outputStream.Seek(0, SeekOrigin.End);

            lumps[j].fourCC = BitConverter.GetBytes(lumps[j].filelen);
            lumps[j].filelen = LZMA_HEADER.Size + lzmaSize;
        }

        //Store GameLumps
        inputStream.Seek(lumps[GAMELUMP_INDEX].fileofs, SeekOrigin.Begin);
        lumps[GAMELUMP_INDEX].fileofs = (uint)outputStream.Position;
        uint gameLumpCount = reader.ReadUInt32();

        DGAMELUMP[] gameLumps = new DGAMELUMP[gameLumpCount];
        for (int k = 0; k < gameLumpCount; k++)
            gameLumps[k] = new DGAMELUMP(reader);

        //When compressed last dGameLump needs to be a dummy structure; creates one more than read
        writer.Write(gameLumpCount + 1);
        outputStream.Seek(DGAMELUMP.Size * (gameLumpCount + 1), SeekOrigin.Current);

        //Compress GameLumps
        foreach (DGAMELUMP gameLump in gameLumps)
        {
            PadTo4Bytes(outputStream, writer);

            inputStream.Seek(gameLump.fileofs, SeekOrigin.Begin);
            gameLump.fileofs = (uint)outputStream.Position;

            outputStream.Seek(LZMA_HEADER.Size, SeekOrigin.Current);
            uint lzmaSize = CompressLZMA(inputStream, outputStream, gameLump.filelen);

            //Patch LZMA header
            outputStream.Seek(gameLump.fileofs, SeekOrigin.Begin);
            new LZMA_HEADER(gameLump.filelen, lzmaSize).Write(writer);
            outputStream.Seek(0, SeekOrigin.End);

            gameLump.flags = 1;
        }

        //Compress pakfile Lump
        long outputPakfileOffset = outputStream.Position;
        foreach (var header in CentralDir)
        {
            inputStream.Seek(inputPakfileOffset + header.relativeOffsetOfLocalHeader, SeekOrigin.Begin);
            ZIP_LocalFileHeader localHeader = new(reader);

            header.relativeOffsetOfLocalHeader = (uint)(outputStream.Position - outputPakfileOffset);

            outputStream.Seek(localHeader.Size, SeekOrigin.Current);

            localHeader.compressedSize = header.compressedSize = CompressLZMA(inputStream, outputStream, header.uncompressedSize, IsPakfile: true);
            localHeader.versionNeededToExtract = header.versionNeededToExtract = VERSION_NEEDED_TO_EXTRACT;
            localHeader.compressionMethod = header.compressionMethod = COMPRESSION_METHOD;

            outputStream.Seek(outputPakfileOffset + header.relativeOffsetOfLocalHeader, SeekOrigin.Begin);
            localHeader.Write(writer);

            outputStream.Seek(0, SeekOrigin.End);
        }

        //Patch BSP lump headers
        outputStream.Seek(8, SeekOrigin.Begin);
        foreach (LUMP lump in lumps)
            lump.Write(writer);

        //Patch GameLump headers
        outputStream.Seek(lumps[GAMELUMP_INDEX].fileofs + 4, SeekOrigin.Begin);
        foreach (DGAMELUMP gameLump in gameLumps)
            gameLump.Write(writer);

        outputStream.Seek(0, SeekOrigin.End);
        return (outputStream, outputPakfileOffset);
    }
}
