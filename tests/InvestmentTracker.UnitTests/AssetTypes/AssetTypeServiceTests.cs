using InvestmentTracker.Application.AssetTypes.Exceptions;
using InvestmentTracker.Application.AssetTypes;
using InvestmentTracker.Application.AssetTypes.Dtos;
using InvestmentTracker.Application.AssetTypes.Interfaces;
using InvestmentTracker.Application.AssetTypes.Services;
using InvestmentTracker.Domain.Entities;
using Moq;

namespace InvestmentTracker.UnitTests.AssetTypes
{
    public class AssetTypeServiceTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAndUpdate_ShouldRejectMissingName(string? name)
        {
            var repository = new Mock<IAssetTypeRepository>(MockBehavior.Strict);
            var service = new AssetTypeService(repository.Object);

            await Assert.ThrowsAsync<AssetTypeValidationException>(
                () => service.CreateAsync(new CreateAssetTypeDto(name!)));
            await Assert.ThrowsAsync<AssetTypeValidationException>(
                () => service.UpdateAsync(1, new UpdateAssetTypeDto(name!)));
            repository.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CreateAndUpdate_ShouldRejectNameOverLimit()
        {
            var repository = new Mock<IAssetTypeRepository>(MockBehavior.Strict);
            var service = new AssetTypeService(repository.Object);
            var name = new string('a', AssetType.NameMaxLength + 1);

            await Assert.ThrowsAsync<AssetTypeValidationException>(
                () => service.CreateAsync(new CreateAssetTypeDto(name)));
            await Assert.ThrowsAsync<AssetTypeValidationException>(
                () => service.UpdateAsync(1, new UpdateAssetTypeDto(name)));
            repository.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CreateAndUpdate_ShouldTrimAndAcceptNameAtLimit()
        {
            var repository = new Mock<IAssetTypeRepository>();
            var entity = new AssetType { Id = 1, Name = "Original" };
            repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
            var service = new AssetTypeService(repository.Object);
            var name = new string('a', AssetType.NameMaxLength);

            var created = await service.CreateAsync(new CreateAssetTypeDto($"  {name}  "));
            var updated = await service.UpdateAsync(1, new UpdateAssetTypeDto($"  {name}  "));

            Assert.Equal(name, created.Name);
            Assert.Equal(name, updated!.Name);
        }

        [Fact]
        public async Task Update_ShouldRejectDuplicateName()
        {
            var repository = new Mock<IAssetTypeRepository>();
            var entity = new AssetType { Id = 1, Name = "Original" };
            repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
            repository.Setup(r => r.ExistsByNameAsync("Duplicado", 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var service = new AssetTypeService(repository.Object);

            await Assert.ThrowsAsync<AssetTypeConflictException>(
                () => service.UpdateAsync(1, new UpdateAssetTypeDto("Duplicado")));

            Assert.Equal("Original", entity.Name);
            repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldCreateAssetType_WhenNameIsValid()
        {
            // Arrange
            var repository = new Mock<IAssetTypeRepository>();

            repository
                .Setup(repository => repository.ExistsByNameAsync(
                    "Ação",
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            repository
                .Setup(repository => repository.AddAsync(
                    It.IsAny<AssetType>(),
                    It.IsAny<CancellationToken>()))
                .Callback<AssetType, CancellationToken>((assetType, _) =>
                {
                    assetType.Id = 1;
                })
                .Returns(Task.CompletedTask);

            repository
                .Setup(repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new AssetTypeService(repository.Object);

            var dto = new CreateAssetTypeDto("Ação");

            // Act
            var result = await service.CreateAsync(dto);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Equal("Ação", result.Name);

            repository.Verify(
                repository => repository.AddAsync(
                    It.Is<AssetType>(assetType =>
                        assetType.Name == "Ação"),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrowArgumentException_WhenNameIsEmpty()
        {
            var repository = new Mock<IAssetTypeRepository>();

            var service = new AssetTypeService(repository.Object);

            var dto = new CreateAssetTypeDto(string.Empty);

            await Assert.ThrowsAsync<AssetTypeValidationException>(
                () => service.CreateAsync(dto));

            repository.Verify(
                repository => repository.AddAsync(
                    It.IsAny<AssetType>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrowArgumentException_WhenNameContainsOnlyWhitespace()
        {
            var repository = new Mock<IAssetTypeRepository>();

            var service = new AssetTypeService(repository.Object);

            var dto = new CreateAssetTypeDto("   ");

            await Assert.ThrowsAsync<AssetTypeValidationException>(
                () => service.CreateAsync(dto));

            repository.Verify(
                repository => repository.AddAsync(
                    It.IsAny<AssetType>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrowInvalidOperationException_WhenNameAlreadyExists()
        {
            var repository = new Mock<IAssetTypeRepository>();

            repository
                .Setup(repository => repository.ExistsByNameAsync(
                    "Ação",
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var service = new AssetTypeService(repository.Object);

            var dto = new CreateAssetTypeDto("Ação");

            await Assert.ThrowsAsync<AssetTypeConflictException>(
                () => service.CreateAsync(dto));

            repository.Verify(
                repository => repository.ExistsByNameAsync(
                    "Ação",
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.AddAsync(
                    It.IsAny<AssetType>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnAssetType_WhenAssetTypeExists()
        {
            var repository = new Mock<IAssetTypeRepository>();

            var assetType = new AssetType
            {
                Id = 1,
                Name = "Ação"
            };

            repository
                .Setup(repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(assetType);

            var service = new AssetTypeService(repository.Object);

            var result = await service.GetByIdAsync(1);

            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Ação", result.Name);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNull_WhenAssetTypeDoesNotExist()
        {
            var repository = new Mock<IAssetTypeRepository>();

            repository
                .Setup(repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AssetType?)null);

            var service = new AssetTypeService(repository.Object);

            var result = await service.GetByIdAsync(999);

            Assert.Null(result);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdateAssetType_WhenAssetTypeExists()
        {
            var repository = new Mock<IAssetTypeRepository>();

            var assetType = new AssetType
            {
                Id = 1,
                Name = "Ação"
            };

            repository
                .Setup(repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(assetType);

            repository
                .Setup(repository => repository.ExistsByNameAsync(
                    "Ações",
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            repository
                .Setup(repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new AssetTypeService(repository.Object);

            var dto = new UpdateAssetTypeDto("Ações");

            var result = await service.UpdateAsync(1, dto);

            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Ações", result.Name);

            Assert.Equal("Ações", assetType.Name);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.ExistsByNameAsync(
                    "Ações",
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNull_WhenAssetTypeDoesNotExist()
        {
            var repository = new Mock<IAssetTypeRepository>();

            repository
                .Setup(repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AssetType?)null);

            var service = new AssetTypeService(repository.Object);

            var dto = new UpdateAssetTypeDto("Ações");

            var result = await service.UpdateAsync(999, dto);

            Assert.Null(result);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.ExistsByNameAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_ShouldDeleteAssetType_WhenAssetTypeExists()
        {
            var repository = new Mock<IAssetTypeRepository>();

            var assetType = new AssetType
            {
                Id = 1,
                Name = "Ação"
            };

            repository
                .Setup(repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(assetType);

            repository
                .Setup(repository => repository.DeleteAsync(
                    assetType,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            repository
                .Setup(repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new AssetTypeService(repository.Object);

            var result = await service.DeleteAsync(1);

            Assert.True(result);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.DeleteAsync(
                    assetType,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnFalse_WhenAssetTypeDoesNotExist()
        {
            var repository = new Mock<IAssetTypeRepository>();

            repository
                .Setup(repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AssetType?)null);

            var service = new AssetTypeService(repository.Object);

            var result = await service.DeleteAsync(999);

            Assert.False(result);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.DeleteAsync(
                    It.IsAny<AssetType>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
