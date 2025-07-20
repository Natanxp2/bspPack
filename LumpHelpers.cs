using System.Formats.Asn1;
using LzmaEncoder = SevenZip.Compression.LZMA.Encoder;

namespace bspPack;

public partial class LumpManager
{
    public static uint CompressLZMA(Stream inputStream, FileStream outputStream, uint filelen, bool IsPakfile = false)
    {
        LzmaEncoder encoder = new();

        encoder.SetCoderProperties([
            SevenZip.CoderPropID.LitContextBits,
            SevenZip.CoderPropID.LitPosBits,
            SevenZip.CoderPropID.PosStateBits,
            SevenZip.CoderPropID.DictionarySize
        ], [LZMA_HEADER.LC, LZMA_HEADER.LP, LZMA_HEADER.PB, LZMA_HEADER.DICT_SIZE]);

        long startPos = outputStream.Position;

        if (IsPakfile)
        {
            //Write version number (9.20) and properties size ( always 5? ) before actual properties
            outputStream.Write([0x09, 0x014, 0x05, 0x00]);
            encoder.WriteCoderProperties(outputStream);
        }

        //filelen argument is not actually enforced by the encoder and so the stream needs to terminate itself
        //WHY THE FUCK is it designed like this. There must be a reason, right?
        if (filelen != 0)
        {
            using SubStream subStreamInput = new(inputStream, inputStream.Position, filelen);
            encoder.Code(subStreamInput, outputStream, filelen, -1, null);
        }
        else
            encoder.Code(inputStream, outputStream, -1, -1, null);

        return (uint)(outputStream.Position - startPos);
    }

    static void PadTo4Bytes(FileStream fs, BinaryWriter writer)
    {
        int padding = (int)(4 - (fs.Position % 4)) % 4;
        if (padding > 0)
            writer.Write(new byte[padding]);
    }

    static void GetEOCD(FileStream fs, BinaryReader reader)
    {
        const int maxSearch = 0xFFFF + 22;
        byte[] buffer = new byte[maxSearch];
        fs.Seek(-Math.Min(maxSearch, fs.Length), SeekOrigin.End);
        fs.ReadExactly(buffer);

        long eocdOffset = 0;

        for (int i = buffer.Length - 22; i >= 0; i--)
        {
            if (buffer[i] == 0x50 && buffer[i + 1] == 0x4B && buffer[i + 2] == 0x05 && buffer[i + 3] == 0x06)
                eocdOffset = fs.Length - buffer.Length + i;
        }

        if (eocdOffset == 0)
            throw new Exception("EOCD not found");

        fs.Seek(eocdOffset, SeekOrigin.Begin);
        EOCD = new ZIP_EndOfCentralDirRecord(reader);
    }
}

public class SubStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _start;
    private readonly long _length;
    private long _position;

    public SubStream(Stream baseStream, long start, long length)
    {
        _baseStream = baseStream;
        _start = start;
        _length = length;
        _position = 0;
        _baseStream.Seek(_start, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long remaining = _length - _position;
        if (remaining <= 0) return 0;

        if (count > remaining)
            count = (int)remaining;

        int read = _baseStream.Read(buffer, offset, count);
        _position += read;
        return read;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}