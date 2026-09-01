using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Domain.Entities
{
    public class CategoryAllocationTarget
    {
        public int Id { get; set; }

        public int PortfolioId { get; set; }

        public int AssetCategoryId { get; set; }

        public decimal TargetPercentage { get; set; }

        public Portfolio Portfolio { get; set; } = null!;

        public AssetCategory AssetCategory { get; set; } = null!;
    }
}
