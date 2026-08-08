using System;
using System.IO;
using System.IO.Compression;

namespace CodeBrix.Texinfo2Pdf.Tests;

/// <summary>
/// Builds a real, decodable PNG from first principles, so the tests that need a picture can have
/// one without the repository carrying a binary fixture nobody can review - and so that a test
/// asserting Html2Pdf decoded a picture is asserting about a picture that really is one.
/// </summary>
internal static class TestPng
{
    /// <summary>Writes a single-colour truecolour PNG of the given size.</summary>
    public static byte[] Build(int width, int height)
    {
        byte[] raw = new byte[height * (1 + (width * 3))];
        int at = 0;
        for (int row = 0; row < height; row++)
        {
            raw[at++] = 0; //No per-row filtering.
            for (int column = 0; column < width; column++)
            {
                raw[at++] = 0x33;
                raw[at++] = 0x55;
                raw[at++] = 0x88;
            }
        }
        byte[] compressed;
        using (MemoryStream buffer = new MemoryStream())
        {
            using (ZLibStream deflate = new ZLibStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(raw, 0, raw.Length);
            }
            compressed = buffer.ToArray();
        }

        byte[] header = new byte[13];
        WriteBigEndian(header, 0, width);
        WriteBigEndian(header, 4, height);
        header[8] = 8;  //Eight bits per channel.
        header[9] = 2;  //Truecolour, no alpha.
        header[10] = 0; //Deflate, the only compression PNG defines.
        header[11] = 0; //Adaptive filtering, the only filter method PNG defines.
        header[12] = 0; //No interlacing.

        using MemoryStream png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed);
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);
        byte[] typeAndData = new byte[4 + data.Length];
        for (int index = 0; index < 4; index++)
        {
            typeAndData[index] = (byte)type[index];
        }
        Array.Copy(data, 0, typeAndData, 4, data.Length);
        stream.Write(typeAndData);
        byte[] crc = new byte[4];
        WriteBigEndian(crc, 0, unchecked((int)Crc32(typeAndData)));
        stream.Write(crc);
    }

    private static void WriteBigEndian(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value >> 24);
        target[offset + 1] = (byte)(value >> 16);
        target[offset + 2] = (byte)(value >> 8);
        target[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        //The CRC-32 that PNG specifies: the reflected polynomial, all ones in and all ones out.
        uint crc = 0xFFFFFFFFu;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        return crc ^ 0xFFFFFFFFu;
    }
}
