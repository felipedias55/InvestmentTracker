using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Domain.Entities
{
    public class Portfolio
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<PortfolioAsset> PortfolioAssets { get; set; } = [];

        public ICollection<CategoryAllocationTarget> CategoryAllocationTargets { get; set; } = [];

        public ICollection<SectorAllocationTarget> SectorAllocationTargets { get; set; } = [];
    }
}
