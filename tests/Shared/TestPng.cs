using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace InvestmentTracker.Testing
{
    internal static class TestPng
    {
        // Small deterministic raster fixture; browser-generated uploads are also tested end to end.
        public static byte[] Create(byte red = 255, int width = 1, int height = 1, int extraPixels = 0, bool corruptCompression = false)
        {
            using var file = new MemoryStream(); file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var header = new byte[13]; BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width); BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)height); header[8] = 8; header[9] = 6;
            Chunk(file, "IHDR", header);
            var pixels = new byte[(width * 4 + 1) * height + extraPixels];
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) { pixels[y * (width * 4 + 1) + x * 4 + 1] = red; pixels[y * (width * 4 + 1) + x * 4 + 4] = 255; }
            using var compressed = new MemoryStream(); using (var z = new ZLibStream(compressed, CompressionLevel.SmallestSize, true)) z.Write(pixels);
            var compressedPixels = compressed.ToArray();
            if (corruptCompression) compressedPixels[0] = 0;
            Chunk(file, "IDAT", compressedPixels); Chunk(file, "IEND", []); return file.ToArray();
        }
        private static void Chunk(Stream file, string type, byte[] payload)
        {
            var word = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(word, (uint)payload.Length); file.Write(word);
            var bytes = Encoding.ASCII.GetBytes(type).Concat(payload).ToArray(); file.Write(bytes);
            uint crc = uint.MaxValue; foreach (var b in bytes) { crc ^= b; for (var i = 0; i < 8; i++) crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xedb88320u; }
            BinaryPrimitives.WriteUInt32BigEndian(word, ~crc); file.Write(word);
        }
    }
}
