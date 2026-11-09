namespace InvestmentTracker.Domain.Entities
{
    public sealed class AssetImage
    {
        public int AssetId { get; set; }
        public Asset Asset { get; set; } = null!;
        public byte[] Data { get; set; } = [];
    }
}
