using InvestmentTracker.Application.AssetTypes.Dtos;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence.Repositories;
using InvestmentTracker.IntegrationTests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace InvestmentTracker.IntegrationTests.AssetTypes
{
    [Collection(DatabaseCollection.Name)]
    public class AssetTypesApiTests
    {
        private readonly DatabaseFixture _fixture;

        public AssetTypesApiTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetAll_ShouldReturnAssetTypes()
        {
            await _fixture.ResetAsync();

            await using var context =
                _fixture.Database.CreateContext();

            var repository =
                new AssetTypeRepository(context);

            await repository.AddAsync(
                new AssetType
                {
                    Name = "Ação"
                });

            await repository.AddAsync(
                new AssetType
                {
                    Name = "FII"
                });

            await repository.SaveChangesAsync();

            using var client =
                _fixture.ApiFactory.CreateClient();

            var response =
                await client.GetAsync("/api/asset-types");

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            var result =
                await response.Content
                    .ReadFromJsonAsync<List<AssetTypeDto>>();

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            Assert.Equal(
                "Ação",
                result[0].Name);

            Assert.Equal(
                "FII",
                result[1].Name);
        }

        [Fact]
        public async Task GetById_ShouldReturnAssetType_WhenAssetTypeExists()
        {
            await _fixture.ResetAsync();

            await using var context =
                _fixture.Database.CreateContext();

            var repository =
                new AssetTypeRepository(context);

            var assetType = new AssetType
            {
                Name = "Ação"
            };

            await repository.AddAsync(assetType);
            await repository.SaveChangesAsync();

            using var client =
                _fixture.ApiFactory.CreateClient();

            var response =
                await client.GetAsync(
                    $"/api/asset-types/{assetType.Id}");

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            var result =
                await response.Content
                    .ReadFromJsonAsync<AssetTypeDto>();

            Assert.NotNull(result);
            Assert.Equal(assetType.Id, result.Id);
            Assert.Equal("Ação", result.Name);
        }

        [Fact]
        public async Task GetById_ShouldReturnNotFound_WhenAssetTypeDoesNotExist()
        {
            await _fixture.ResetAsync();

            using var client =
                _fixture.ApiFactory.CreateClient();

            var response =
                await client.GetAsync(
                    "/api/asset-types/999");

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);
        }

        [Fact]
        public async Task Create_ShouldReturnCreated_WhenAssetTypeIsValid()
        {
            await _fixture.ResetAsync();

            using var client =
                _fixture.ApiFactory.CreateClient();

            var request = new CreateAssetTypeDto("Ação");

            var response =
                await client.PostAsJsonAsync(
                    "/api/asset-types",
                    request);

            Assert.Equal(
                HttpStatusCode.Created,
                response.StatusCode);

            var result =
                await response.Content
                    .ReadFromJsonAsync<AssetTypeDto>();

            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.Equal("Ação", result.Name);

            Assert.NotNull(response.Headers.Location);

            Assert.Contains(
                $"/api/asset-types/{result.Id}",
                response.Headers.Location.ToString());
        }

        [Fact]
        public async Task Create_ShouldReturnConflict_WhenAssetTypeNameAlreadyExists()
        {
            await _fixture.ResetAsync();

            await using var context =
                _fixture.Database.CreateContext();

            var repository =
                new AssetTypeRepository(context);

            await repository.AddAsync(
                new AssetType
                {
                    Name = "Ação"
                });

            await repository.SaveChangesAsync();

            using var client =
                _fixture.ApiFactory.CreateClient();

            var request =
                new CreateAssetTypeDto("Ação");

            var response =
                await client.PostAsJsonAsync(
                    "/api/asset-types",
                    request);

            Assert.Equal(
                HttpStatusCode.Conflict,
                response.StatusCode);

            var content =
                await response.Content.ReadAsStringAsync();

            Assert.Contains(
                "Já existe um tipo de ativo com esse nome.",
                content);
        }

        [Fact]
        public async Task Create_ShouldReturnBadRequest_WhenAssetTypeNameIsEmpty()
        {
            await _fixture.ResetAsync();

            using var client =
                _fixture.ApiFactory.CreateClient();

            var request =
                new CreateAssetTypeDto(string.Empty);

            var response =
                await client.PostAsJsonAsync(
                    "/api/asset-types",
                    request);

            Assert.Equal(
                HttpStatusCode.BadRequest,
                response.StatusCode);

            var content =
                await response.Content.ReadAsStringAsync();

            Assert.Contains(
                "O nome do tipo de ativo é obrigatório.",
                content);
        }

        [Fact]
        public async Task Update_ShouldReturnOk_WhenAssetTypeExists()
        {
            await _fixture.ResetAsync();

            await using var context =
                _fixture.Database.CreateContext();

            var repository =
                new AssetTypeRepository(context);

            var assetType = new AssetType
            {
                Name = "Ação"
            };

            await repository.AddAsync(assetType);
            await repository.SaveChangesAsync();

            using var client =
                _fixture.ApiFactory.CreateClient();

            var request =
                new UpdateAssetTypeDto("Ações");

            var response =
                await client.PutAsJsonAsync(
                    $"/api/asset-types/{assetType.Id}",
                    request);

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            var result =
                await response.Content
                    .ReadFromJsonAsync<AssetTypeDto>();

            Assert.NotNull(result);
            Assert.Equal(assetType.Id, result.Id);
            Assert.Equal("Ações", result.Name);

            await using var verificationContext =
                _fixture.Database.CreateContext();

            var verificationRepository =
                new AssetTypeRepository(verificationContext);

            var persisted =
                await verificationRepository.GetByIdAsync(assetType.Id);

            Assert.NotNull(persisted);
            Assert.Equal("Ações", persisted.Name);
        }

        [Fact]
        public async Task Update_ShouldReturnNotFound_WhenAssetTypeDoesNotExist()
        {
            await _fixture.ResetAsync();

            using var client =
                _fixture.ApiFactory.CreateClient();

            var request =
                new UpdateAssetTypeDto("Ações");

            var response =
                await client.PutAsJsonAsync(
                    "/api/asset-types/999",
                    request);

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);
        }

        [Fact]
        public async Task Delete_ShouldReturnNoContent_WhenAssetTypeExists()
        {
            await _fixture.ResetAsync();

            int assetTypeId;

            await using (var context =
                _fixture.Database.CreateContext())
            {
                var repository =
                    new AssetTypeRepository(context);

                var assetType = new AssetType
                {
                    Name = "Ação"
                };

                await repository.AddAsync(assetType);
                await repository.SaveChangesAsync();

                assetTypeId = assetType.Id;
            }

            using var client =
                _fixture.ApiFactory.CreateClient();

            var response =
                await client.DeleteAsync(
                    $"/api/asset-types/{assetTypeId}");

            Assert.Equal(
                HttpStatusCode.NoContent,
                response.StatusCode);

            await using var verificationContext =
                _fixture.Database.CreateContext();

            var verificationRepository =
                new AssetTypeRepository(verificationContext);

            var persisted =
                await verificationRepository.GetByIdAsync(assetTypeId);

            Assert.Null(persisted);
        }

        [Fact]
        public async Task Delete_ShouldReturnNotFound_WhenAssetTypeDoesNotExist()
        {
            await _fixture.ResetAsync();

            using var client =
                _fixture.ApiFactory.CreateClient();

            var response =
                await client.DeleteAsync(
                    "/api/asset-types/999");

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);
        }
    }
}
