using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Domain.Entities
{
    public class SectorAllocationTarget
    {
        public int Id { get; set; }

        public int PortfolioId { get; set; }

        public int SectorId { get; set; }

        public decimal TargetPercentage { get; set; }

        public Portfolio Portfolio { get; set; } = null!;

        public Sector Sector { get; set; } = null!;
    }
}
