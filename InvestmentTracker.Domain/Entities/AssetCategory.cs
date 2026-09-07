using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Domain.Entities
{
    public class AssetCategory
    {
        public const int NameMaxLength = 100;

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public ICollection<Asset> Assets { get; set; } = [];

        public ICollection<CategoryAllocationTarget> AllocationTargets { get; set; } = [];
    }
}
