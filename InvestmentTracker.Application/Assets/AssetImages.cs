using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Assets
{
    public sealed record AssetImageChange(byte[]? Data, string? ExpectedVersion = null);
    public sealed record AssetImageFile(byte[] Data, string Version);
    public interface IAssetImageRepository
    {
        Task<AssetImageFile?> ReadAsync(int assetId, CancellationToken ct);
        Task StageAsync(Asset asset, byte[]? data, CancellationToken ct);
    }
    public static class AssetImageRules
    {
        public const int MaximumBytes = 262144;
        public static string? Validate(AssetImageChange change, string? currentVersion)
        {
            string? version = null;
            if (change.Data is not null)
            {
                ValidatePng(change.Data);
                version = Convert.ToHexStringLower(SHA256.HashData(change.Data));
            }
            // A repeated upload of the same content is safe after an uncertain response.
            if (currentVersion != change.ExpectedVersion && version != currentVersion)
                throw new ResourceConflictException("A imagem do ativo mudou. Recarregue o cadastro antes de substituir ou remover.");
            return version;
        }
        public static void ValidatePng(byte[] data)
        {
            void Invalid() => throw new InputValidationException("Imagem inválida. Selecione novamente uma imagem PNG, JPEG ou WebP pelo editor do ativo.");
            if (data.Length is < 57 or > MaximumBytes || !data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) Invalid();
            var offset = 8; var width = 0; var height = 0; var channels = 0; var ended = false;
            using var compressed = new MemoryStream();
            while (offset <= data.Length - 12)
            {
                var length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
                if (length > data.Length - offset - 12) Invalid();
                var count = (int)length;
                var type = System.Text.Encoding.ASCII.GetString(data, offset + 4, 4);
                var payload = data.AsSpan(offset + 8, count);
                if (Crc(data.AsSpan(offset + 4, count + 4)) != BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8 + count, 4))) Invalid();
                if (offset == 8 && type != "IHDR") Invalid();
                if (type == "IHDR")
                {
                    if (offset != 8 || count != 13) Invalid();
                    var w = BinaryPrimitives.ReadUInt32BigEndian(payload[..4]); var h = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4));
                    if (w is < 1 or > 256 || h is < 1 or > 256 || payload[8] != 8 || payload[9] is not (2 or 6) || payload[10] != 0 || payload[11] != 0 || payload[12] != 0) Invalid();
                    width = (int)w; height = (int)h; channels = payload[9] == 6 ? 4 : 3;
                }
                else if (type == "IDAT") compressed.Write(payload);
                else if (type == "IEND") { if (count != 0) Invalid(); ended = true; offset += 12; break; }
                else if ((data[offset + 4] & 32) == 0) Invalid();
                offset += count + 12;
            }
            if (!ended || offset != data.Length || width == 0 || compressed.Length == 0) Invalid();
            compressed.Position = 0;
            try
            {
                using var inflated = new ZLibStream(compressed, CompressionMode.Decompress);
                var row = new byte[width * channels + 1];
                for (var y = 0; y < height; y++) { inflated.ReadExactly(row); if (row[0] > 4) Invalid(); }
                if (inflated.ReadByte() != -1) Invalid();
            }
            catch (Exception error) when (error is IOException or InvalidDataException) { Invalid(); }
        }
        private static uint Crc(ReadOnlySpan<byte> data)
        {
            uint value = uint.MaxValue;
            foreach (var b in data) { value ^= b; for (var i = 0; i < 8; i++) value = (value & 1) != 0 ? (value >> 1) ^ 0xedb88320u : value >> 1; }
            return ~value;
        }
    }
}
