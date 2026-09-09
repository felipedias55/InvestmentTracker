using InvestmentTracker.Application.History.Interfaces;
using InvestmentTracker.Application.History.Services;
using InvestmentTracker.Application.ExternalAssets.Interfaces;
using InvestmentTracker.Application.ExternalAssets.Services;
using InvestmentTracker.Application.Allocation.Interfaces;
using InvestmentTracker.Application.Allocation.Services;
using InvestmentTracker.Application.Portfolios;
using InvestmentTracker.Application.Portfolios.Interfaces;
using InvestmentTracker.Application.Portfolios.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using InvestmentTracker.Application.AssetCategories.Interfaces;
using InvestmentTracker.Application.AssetCategories.Services;
using InvestmentTracker.Application.AssetTypes.Interfaces;
using InvestmentTracker.Application.AssetTypes.Services;
using InvestmentTracker.Application.Assets.Interfaces;
using InvestmentTracker.Application.Assets.Services;
using InvestmentTracker.Application.Countries.Interfaces;
using InvestmentTracker.Application.Countries.Services;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.Currencies.Services;
using InvestmentTracker.Application.Sectors.Interfaces;
using InvestmentTracker.Application.Sectors.Services;
using Microsoft.Extensions.DependencyInjection;

namespace InvestmentTracker.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(
            this IServiceCollection services)
        {
            services.AddScoped<
                IAssetTypeService,
                AssetTypeService>();

            services.AddScoped<ICountryService, CountryService>();

            services.AddScoped<ICurrencyService, CurrencyService>();

            services.AddScoped<IAssetCategoryService, AssetCategoryService>();

            services.AddScoped<ISectorService, SectorService>();

            services.AddScoped<IAssetService, AssetService>();

            services.TryAddSingleton(new PortfolioDefaults());
            services.AddScoped<IPortfolioService, PortfolioService>();

            services.AddScoped<IAllocationService, AllocationService>();
            services.AddScoped<IExternalAssetService, ExternalAssetService>();
            services.AddScoped<IHistoryService, HistoryService>();
            services.AddScoped<InvestmentTracker.Application.Trades.ITradeService, InvestmentTracker.Application.Trades.TradeService>();
            services.AddScoped<InvestmentTracker.Application.Income.IIncomeService, InvestmentTracker.Application.Income.IncomeService>();
            return services;
        }
    }
}
