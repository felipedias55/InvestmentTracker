using InvestmentTracker.Application.AssetTypes.Interfaces;
using InvestmentTracker.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services)
        {
            services.AddScoped<IAssetTypeRepository, AssetTypeRepository>();

            return services;
        }
    }
}
