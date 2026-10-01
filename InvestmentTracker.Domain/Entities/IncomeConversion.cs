namespace InvestmentTracker.Domain.Entities
{
    public sealed class IncomeConversion
    {
        public int Id { get; set; }
        public int IncomeReceiptId { get; set; }
        public int Revision { get; set; }
        public Guid RequestId { get; set; }
        public string BaseCurrencyCode { get; set; } = string.Empty;
        public decimal BaseAmount { get; set; }
        public decimal? Rate { get; set; }
        public DateOnly RateDate { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }
}
