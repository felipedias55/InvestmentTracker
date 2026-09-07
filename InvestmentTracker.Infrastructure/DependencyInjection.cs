using InvestmentTracker.Application.AssetCategories.Interfaces;
using InvestmentTracker.Application.AssetTypes.Interfaces;
using InvestmentTracker.Application.Assets.Interfaces;
using InvestmentTracker.Application.Countries.Interfaces;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.Sectors.Interfaces;
using InvestmentTracker.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace InvestmentTracker.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services)
        {
            services.AddScoped<IAssetTypeRepository, AssetTypeRepository>();

            services.AddScoped<ICountryRepository, CountryRepository>();

            services.AddScoped<ICurrencyRepository, CurrencyRepository>();

            services.AddScoped<IAssetCategoryRepository, AssetCategoryRepository>();

            services.AddScoped<ISectorRepository, SectorRepository>();

            services.AddScoped<IAssetRepository, AssetRepository>();

            return services;
        }
    }
}
