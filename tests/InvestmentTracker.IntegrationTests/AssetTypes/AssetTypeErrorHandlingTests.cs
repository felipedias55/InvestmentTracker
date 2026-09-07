using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InvestmentTracker.Application.AssetTypes.Dtos;
using InvestmentTracker.Application.AssetTypes.Exceptions;
using InvestmentTracker.Application.AssetTypes.Interfaces;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace InvestmentTracker.IntegrationTests.AssetTypes
{
    public class AssetTypeErrorHandlingTests
    {
        [Theory]
        [InlineData("validation", HttpStatusCode.BadRequest)]
        [InlineData("conflict", HttpStatusCode.Conflict)]
        [InlineData("unexpected", HttpStatusCode.InternalServerError)]
        public async Task Api_ShouldMapOnlyKnownErrors(string kind, HttpStatusCode expected)
        {
            Exception error = kind switch
            {
                "validation" => new AssetTypeValidationException("Nome inválido."),
                "conflict" => new AssetTypeConflictException("Tipo em uso."),
                _ => new InvalidOperationException("Internal diagnostic details")
            };
            using var factory = new InvestmentTrackerApiFactory(
                "Server=unused;Database=unused;Integrated Security=true")
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Production");
                    builder.ConfigureTestServices(services =>
                        services.AddScoped<IAssetTypeService>(_ => new FailingService(error)));
                });
            using var client = factory.CreateClient();

            // Exercise all mutation endpoints: none should need local try/catch blocks.
            foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
            {
                using var request = new HttpRequestMessage(method,
                    method == HttpMethod.Post ? "/api/asset-types" : "/api/asset-types/1");
                if (method != HttpMethod.Delete)
                {
                    request.Content = JsonContent.Create(new CreateAssetTypeDto("Nome"));
                }
                using var response = await client.SendAsync(request);
                Assert.Equal(expected, response.StatusCode);
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                var body = await response.Content.ReadAsStringAsync();
                if (kind == "unexpected")
                {
                    Assert.DoesNotContain(error.Message, body);
                }
                else
                {
                    using var json = JsonDocument.Parse(body);
                    Assert.Equal(error.Message, json.RootElement.GetProperty("detail").GetString());
                    Assert.Equal(error.Message, json.RootElement.GetProperty("message").GetString());
                }
            }
        }

        private sealed class FailingService(Exception error) : IAssetTypeService
        {
            public Task<IReadOnlyList<AssetTypeDto>> GetAllAsync(CancellationToken cancellationToken = default)
                => throw error;
            public Task<AssetTypeDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
                => throw error;
            public Task<AssetTypeDto> CreateAsync(CreateAssetTypeDto dto, CancellationToken cancellationToken = default)
                => throw error;
            public Task<AssetTypeDto?> UpdateAsync(int id, UpdateAssetTypeDto dto, CancellationToken cancellationToken = default)
                => throw error;
            public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
                => throw error;
        }
    }
}
