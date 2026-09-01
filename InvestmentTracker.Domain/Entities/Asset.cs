using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Domain.Entities
{
    public class Asset
    {
        public int Id { get; set; }

        public string Ticker { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int AssetTypeId { get; set; }

        public int CountryId { get; set; }

        public int CurrencyId { get; set; }

        public int AssetCategoryId { get; set; }

        public int SectorId { get; set; }

        public DateTime CreatedAt { get; set; }

        public AssetType AssetType { get; set; } = null!;

        public Country Country { get; set; } = null!;

        public Currency Currency { get; set; } = null!;

        public AssetCategory AssetCategory { get; set; } = null!;

        public Sector Sector { get; set; } = null!;

        public ICollection<PortfolioAsset> PortfolioAssets { get; set; } = [];
    }
}
