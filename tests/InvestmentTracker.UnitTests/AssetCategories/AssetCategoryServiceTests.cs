using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.AssetCategories;
using InvestmentTracker.Application.AssetCategories.Dtos;
using InvestmentTracker.Application.AssetCategories.Interfaces;
using InvestmentTracker.Application.AssetCategories.Services;
using InvestmentTracker.Domain.Entities;
using Moq;

namespace InvestmentTracker.UnitTests.AssetCategories
{
    public class AssetCategoryServiceTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAndUpdate_ShouldRejectMissingName(string? name)
        {
            var repository = new Mock<IAssetCategoryRepository>(MockBehavior.Strict);
            var service = new AssetCategoryService(repository.Object);

            await Assert.ThrowsAsync<InputValidationException>(
                () => service.CreateAsync(new CreateAssetCategoryDto(name!)));
            await Assert.ThrowsAsync<InputValidationException>(
                () => service.UpdateAsync(1, new UpdateAssetCategoryDto(name!)));
            repository.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CreateAndUpdate_ShouldRejectNameOverLimit()
        {
            var repository = new Mock<IAssetCategoryRepository>(MockBehavior.Strict);
            var service = new AssetCategoryService(repository.Object);
            var name = new string('a', AssetCategory.NameMaxLength + 1);

            await Assert.ThrowsAsync<InputValidationException>(
                () => service.CreateAsync(new CreateAssetCategoryDto(name)));
            await Assert.ThrowsAsync<InputValidationException>(
                () => service.UpdateAsync(1, new UpdateAssetCategoryDto(name)));
            repository.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CreateAndUpdate_ShouldTrimAndAcceptNameAtLimit()
        {
            var repository = new Mock<IAssetCategoryRepository>();
            var entity = new AssetCategory { Id = 1, Name = "Original" };
            repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
            var service = new AssetCategoryService(repository.Object);
            var name = new string('a', AssetCategory.NameMaxLength);

            var created = await service.CreateAsync(new CreateAssetCategoryDto($"  {name}  "));
            var updated = await service.UpdateAsync(1, new UpdateAssetCategoryDto($"  {name}  "));

            Assert.Equal(name, created.Name);
            Assert.Equal(name, updated!.Name);
        }

        [Fact]
        public async Task Update_ShouldRejectDuplicateName()
        {
            var repository = new Mock<IAssetCategoryRepository>();
            var entity = new AssetCategory { Id = 1, Name = "Original" };
            repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
            repository.Setup(r => r.ExistsByNameAsync("Duplicado", 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var service = new AssetCategoryService(repository.Object);

            await Assert.ThrowsAsync<ResourceConflictException>(
                () => service.UpdateAsync(1, new UpdateAssetCategoryDto("Duplicado")));

            Assert.Equal("Original", entity.Name);
            repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldCreateAssetCategory_WhenNameIsValid()
        {
            // Arrange
            var repository = new Mock<IAssetCategoryRepository>();

            repository
                .Setup(repository => repository.ExistsByNameAsync(
                    "Ações BR",
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            repository
                .Setup(repository => repository.AddAsync(
                    It.IsAny<AssetCategory>(),
                    It.IsAny<CancellationToken>()))
                .Callback<AssetCategory, CancellationToken>((item, _) =>
                {
                    item.Id = 1;
                })
                .Returns(Task.CompletedTask);

            repository
                .Setup(repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new AssetCategoryService(repository.Object);

            var dto = new CreateAssetCategoryDto("Ações BR");

            // Act
            var result = await service.CreateAsync(dto);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Equal("Ações BR", result.Name);

            repository.Verify(
                repository => repository.AddAsync(
                    It.Is<AssetCategory>(item =>
                        item.Name == "Ações BR"),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrowValidationException_WhenNameIsEmpty()
        {
            var repository = new Mock<IAssetCategoryRepository>();

            var service = new AssetCategoryService(repository.Object);

            var dto = new CreateAssetCategoryDto(string.Empty);

            await Assert.ThrowsAsync<InputValidationException>(
                () => service.CreateAsync(dto));

            repository.Verify(
                repository => repository.AddAsync(
                    It.IsAny<AssetCategory>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrowValidationException_WhenNameContainsOnlyWhitespace()
        {
            var repository = new Mock<IAssetCategoryRepository>();

            var service = new AssetCategoryService(repository.Object);

            var dto = new CreateAssetCategoryDto("   ");

            await Assert.ThrowsAsync<InputValidationException>(
                () => service.CreateAsync(dto));

            repository.Verify(
                repository => repository.AddAsync(
                    It.IsAny<AssetCategory>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrowConflictException_WhenNameAlreadyExists()
        {
            var repository = new Mock<IAssetCategoryRepository>();

            repository
                .Setup(repository => repository.ExistsByNameAsync(
                    "Ações BR",
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var service = new AssetCategoryService(repository.Object);

            var dto = new CreateAssetCategoryDto("Ações BR");

            await Assert.ThrowsAsync<ResourceConflictException>(
                () => service.CreateAsync(dto));

            repository.Verify(
                repository => repository.ExistsByNameAsync(
                    "Ações BR",
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.AddAsync(
                    It.IsAny<AssetCategory>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnAssetCategory_WhenAssetCategoryExists()
        {
            var repository = new Mock<IAssetCategoryRepository>();

            var item = new AssetCategory
            {
                Id = 1,
                Name = "Ações BR"
            };

            repository
                .Setup(repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            var service = new AssetCategoryService(repository.Object);

            var result = await service.GetByIdAsync(1);

            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Ações BR", result.Name);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNull_WhenAssetCategoryDoesNotExist()
        {
            var repository = new Mock<IAssetCategoryRepository>();

            repository
                .Setup(repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AssetCategory?)null);

            var service = new AssetCategoryService(repository.Object);

            var result = await service.GetByIdAsync(999);

            Assert.Null(result);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdateAssetCategory_WhenAssetCategoryExists()
        {
            var repository = new Mock<IAssetCategoryRepository>();

            var item = new AssetCategory
            {
                Id = 1,
                Name = "Ações BR"
            };

            repository
                .Setup(repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            repository
                .Setup(repository => repository.ExistsByNameAsync(
                    "Ações BR atualizado",
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            repository
                .Setup(repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new AssetCategoryService(repository.Object);

            var dto = new UpdateAssetCategoryDto("Ações BR atualizado");

            var result = await service.UpdateAsync(1, dto);

            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Ações BR atualizado", result.Name);

            Assert.Equal("Ações BR atualizado", item.Name);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.ExistsByNameAsync(
                    "Ações BR atualizado",
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNull_WhenAssetCategoryDoesNotExist()
        {
            var repository = new Mock<IAssetCategoryRepository>();

            repository
                .Setup(repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AssetCategory?)null);

            var service = new AssetCategoryService(repository.Object);

            var dto = new UpdateAssetCategoryDto("Ações BR atualizado");

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
        public async Task DeleteAsync_ShouldDeleteAssetCategory_WhenAssetCategoryExists()
        {
            var repository = new Mock<IAssetCategoryRepository>();

            var item = new AssetCategory
            {
                Id = 1,
                Name = "Ações BR"
            };

            repository
                .Setup(repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            repository
                .Setup(repository => repository.DeleteAsync(
                    item,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            repository
                .Setup(repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new AssetCategoryService(repository.Object);

            var result = await service.DeleteAsync(1);

            Assert.True(result);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.DeleteAsync(
                    item,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnFalse_WhenAssetCategoryDoesNotExist()
        {
            var repository = new Mock<IAssetCategoryRepository>();

            repository
                .Setup(repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AssetCategory?)null);

            var service = new AssetCategoryService(repository.Object);

            var result = await service.DeleteAsync(999);

            Assert.False(result);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.DeleteAsync(
                    It.IsAny<AssetCategory>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
