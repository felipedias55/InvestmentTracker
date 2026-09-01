using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Domain.Entities
{
    public class PortfolioAsset
    {
        public int Id { get; set; }

        public int PortfolioId { get; set; }

        public int AssetId { get; set; }

        public decimal Quantity { get; set; }

        public decimal InvestedAmount { get; set; }

        public decimal CurrentValue { get; set; }

        public Portfolio Portfolio { get; set; } = null!;

        public Asset Asset { get; set; } = null!;
    }
}
