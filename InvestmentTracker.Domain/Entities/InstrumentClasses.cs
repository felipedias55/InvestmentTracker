namespace InvestmentTracker.Domain.Entities
{
    public static class InstrumentClasses
    {
        public const string General = "general";
        public const string Fii = "fii";
        public const string Reit = "reit";
        public const string Unclassified = "unclassified";
        public static bool IsValid(string? value) => value is General or Fii or Reit or Unclassified;
        public static bool CanReceiveContribution(string? value, bool realEstateSector)
            => value == General || ((value is Fii or Reit) && realEstateSector);
    }
}
