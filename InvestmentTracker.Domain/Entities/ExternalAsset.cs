using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Domain.Entities
{
    public class ExternalAsset
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Value { get; set; }

        public string? Description { get; set; }
    }
}
