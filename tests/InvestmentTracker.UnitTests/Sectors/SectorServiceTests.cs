using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Sectors;
using InvestmentTracker.Application.Sectors.Dtos;
using InvestmentTracker.Application.Sectors.Interfaces;
using InvestmentTracker.Application.Sectors.Services;
using InvestmentTracker.Domain.Entities;
using Moq;

namespace InvestmentTracker.UnitTests.Sectors
{
    public class SectorServiceTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAndUpdate_ShouldRejectMissingName(string? name)
        {
            var repository = new Mock<ISectorRepository>(MockBehavior.Strict);
            var service = new SectorService(repository.Object);

            await Assert.ThrowsAsync<InputValidationException>(
                () => service.CreateAsync(new CreateSectorDto(name!)));
            await Assert.ThrowsAsync<InputValidationException>(
                () => service.UpdateAsync(1, new UpdateSectorDto(name!)));
            repository.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CreateAndUpdate_ShouldRejectNameOverLimit()
        {
            var repository = new Mock<ISectorRepository>(MockBehavior.Strict);
            var service = new SectorService(repository.Object);
            var name = new string('a', Sector.NameMaxLength + 1);

            await Assert.ThrowsAsync<InputValidationException>(
                () => service.CreateAsync(new CreateSectorDto(name)));
            await Assert.ThrowsAsync<InputValidationException>(
                () => service.UpdateAsync(1, new UpdateSectorDto(name)));
            repository.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CreateAndUpdate_ShouldTrimAndAcceptNameAtLimit()
        {
            var repository = new Mock<ISectorRepository>();
            var entity = new Sector { Id = 1, Name = "Original" };
            repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
            var service = new SectorService(repository.Object);
            var name = new string('a', Sector.NameMaxLength);

            var created = await service.CreateAsync(new CreateSectorDto($"  {name}  "));
            var updated = await service.UpdateAsync(1, new UpdateSectorDto($"  {name}  "));

            Assert.Equal(name, created.Name);
            Assert.Equal(name, updated!.Name);
        }

        [Fact]
        public async Task Update_ShouldRejectDuplicateName()
        {
            var repository = new Mock<ISectorRepository>();
            var entity = new Sector { Id = 1, Name = "Original" };
            repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
            repository.Setup(r => r.ExistsByNameAsync("Duplicado", 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var service = new SectorService(repository.Object);

            await Assert.ThrowsAsync<ResourceConflictException>(
                () => service.UpdateAsync(1, new UpdateSectorDto("Duplicado")));

            Assert.Equal("Original", entity.Name);
            repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldCreateSector_WhenNameIsValid()
        {
            // Arrange
            var repository = new Mock<ISectorRepository>();

            repository
                .Setup(repository => repository.ExistsByNameAsync(
                    "Energia",
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            repository
                .Setup(repository => repository.AddAsync(
                    It.IsAny<Sector>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Sector, CancellationToken>((item, _) =>
                {
                    item.Id = 1;
                })
                .Returns(Task.CompletedTask);

            repository
                .Setup(repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new SectorService(repository.Object);

            var dto = new CreateSectorDto("Energia");

            // Act
            var result = await service.CreateAsync(dto);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Equal("Energia", result.Name);

            repository.Verify(
                repository => repository.AddAsync(
                    It.Is<Sector>(item =>
                        item.Name == "Energia"),
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
            var repository = new Mock<ISectorRepository>();

            var service = new SectorService(repository.Object);

            var dto = new CreateSectorDto(string.Empty);

            await Assert.ThrowsAsync<InputValidationException>(
                () => service.CreateAsync(dto));

            repository.Verify(
                repository => repository.AddAsync(
                    It.IsAny<Sector>(),
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
            var repository = new Mock<ISectorRepository>();

            var service = new SectorService(repository.Object);

            var dto = new CreateSectorDto("   ");

            await Assert.ThrowsAsync<InputValidationException>(
                () => service.CreateAsync(dto));

            repository.Verify(
                repository => repository.AddAsync(
                    It.IsAny<Sector>(),
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
            var repository = new Mock<ISectorRepository>();

            repository
                .Setup(repository => repository.ExistsByNameAsync(
                    "Energia",
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var service = new SectorService(repository.Object);

            var dto = new CreateSectorDto("Energia");

            await Assert.ThrowsAsync<ResourceConflictException>(
                () => service.CreateAsync(dto));

            repository.Verify(
                repository => repository.ExistsByNameAsync(
                    "Energia",
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.AddAsync(
                    It.IsAny<Sector>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnSector_WhenSectorExists()
        {
            var repository = new Mock<ISectorRepository>();

            var item = new Sector
            {
                Id = 1,
                Name = "Energia"
            };

            repository
                .Setup(repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            var service = new SectorService(repository.Object);

            var result = await service.GetByIdAsync(1);

            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Energia", result.Name);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNull_WhenSectorDoesNotExist()
        {
            var repository = new Mock<ISectorRepository>();

            repository
                .Setup(repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Sector?)null);

            var service = new SectorService(repository.Object);

            var result = await service.GetByIdAsync(999);

            Assert.Null(result);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdateSector_WhenSectorExists()
        {
            var repository = new Mock<ISectorRepository>();

            var item = new Sector
            {
                Id = 1,
                Name = "Energia"
            };

            repository
                .Setup(repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            repository
                .Setup(repository => repository.ExistsByNameAsync(
                    "Energia atualizado",
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            repository
                .Setup(repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new SectorService(repository.Object);

            var dto = new UpdateSectorDto("Energia atualizado");

            var result = await service.UpdateAsync(1, dto);

            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Energia atualizado", result.Name);

            Assert.Equal("Energia atualizado", item.Name);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.ExistsByNameAsync(
                    "Energia atualizado",
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNull_WhenSectorDoesNotExist()
        {
            var repository = new Mock<ISectorRepository>();

            repository
                .Setup(repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Sector?)null);

            var service = new SectorService(repository.Object);

            var dto = new UpdateSectorDto("Energia atualizado");

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
        public async Task DeleteAsync_ShouldDeleteSector_WhenSectorExists()
        {
            var repository = new Mock<ISectorRepository>();

            var item = new Sector
            {
                Id = 1,
                Name = "Energia"
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

            var service = new SectorService(repository.Object);

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
        public async Task DeleteAsync_ShouldReturnFalse_WhenSectorDoesNotExist()
        {
            var repository = new Mock<ISectorRepository>();

            repository
                .Setup(repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Sector?)null);

            var service = new SectorService(repository.Object);

            var result = await service.DeleteAsync(999);

            Assert.False(result);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.DeleteAsync(
                    It.IsAny<Sector>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
