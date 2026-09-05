using InvestmentTracker.Application.AssetTypes.Interfaces;
using InvestmentTracker.Application.AssetTypes.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

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

            return services;
        }
    }
}
