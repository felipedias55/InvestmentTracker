using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Countries;
using InvestmentTracker.Application.Countries.Dtos;
using InvestmentTracker.Application.Countries.Interfaces;
using InvestmentTracker.Application.Countries.Services;
using InvestmentTracker.Domain.Entities;
using Moq;

namespace InvestmentTracker.UnitTests.Countries
{
    public class CountryServiceTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAndUpdate_ShouldRejectMissingName(string? name)
        {
            var repository = new Mock<ICountryRepository>(MockBehavior.Strict);
            var service = new CountryService(repository.Object);

            await Assert.ThrowsAsync<InputValidationException>(
                () => service.CreateAsync(new CreateCountryDto(name!)));
            await Assert.ThrowsAsync<InputValidationException>(
                () => service.UpdateAsync(1, new UpdateCountryDto(name!)));
            repository.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CreateAndUpdate_ShouldRejectNameOverLimit()
        {
            var repository = new Mock<ICountryRepository>(MockBehavior.Strict);
            var service = new CountryService(repository.Object);
            var name = new string('a', Country.NameMaxLength + 1);

            await Assert.ThrowsAsync<InputValidationException>(
                () => service.CreateAsync(new CreateCountryDto(name)));
            await Assert.ThrowsAsync<InputValidationException>(
                () => service.UpdateAsync(1, new UpdateCountryDto(name)));
            repository.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CreateAndUpdate_ShouldTrimAndAcceptNameAtLimit()
        {
            var repository = new Mock<ICountryRepository>();
            var entity = new Country { Id = 1, Name = "Original" };
            repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
            var service = new CountryService(repository.Object);
            var name = new string('a', Country.NameMaxLength);

            var created = await service.CreateAsync(new CreateCountryDto($"  {name}  "));
            var updated = await service.UpdateAsync(1, new UpdateCountryDto($"  {name}  "));

            Assert.Equal(name, created.Name);
            Assert.Equal(name, updated!.Name);
        }

        [Fact]
        public async Task Update_ShouldRejectDuplicateName()
        {
            var repository = new Mock<ICountryRepository>();
            var entity = new Country { Id = 1, Name = "Original" };
            repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
            repository.Setup(r => r.ExistsByNameAsync("Duplicado", 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var service = new CountryService(repository.Object);

            await Assert.ThrowsAsync<ResourceConflictException>(
                () => service.UpdateAsync(1, new UpdateCountryDto("Duplicado")));

            Assert.Equal("Original", entity.Name);
            repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_ShouldCreateCountry_WhenNameIsValid()
        {
            // Arrange
            var repository = new Mock<ICountryRepository>();

            repository
                .Setup(repository => repository.ExistsByNameAsync(
                    "Brasil",
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            repository
                .Setup(repository => repository.AddAsync(
                    It.IsAny<Country>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Country, CancellationToken>((item, _) =>
                {
                    item.Id = 1;
                })
                .Returns(Task.CompletedTask);

            repository
                .Setup(repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new CountryService(repository.Object);

            var dto = new CreateCountryDto("Brasil");

            // Act
            var result = await service.CreateAsync(dto);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Equal("Brasil", result.Name);

            repository.Verify(
                repository => repository.AddAsync(
                    It.Is<Country>(item =>
                        item.Name == "Brasil"),
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
            var repository = new Mock<ICountryRepository>();

            var service = new CountryService(repository.Object);

            var dto = new CreateCountryDto(string.Empty);

            await Assert.ThrowsAsync<InputValidationException>(
                () => service.CreateAsync(dto));

            repository.Verify(
                repository => repository.AddAsync(
                    It.IsAny<Country>(),
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
            var repository = new Mock<ICountryRepository>();

            var service = new CountryService(repository.Object);

            var dto = new CreateCountryDto("   ");

            await Assert.ThrowsAsync<InputValidationException>(
                () => service.CreateAsync(dto));

            repository.Verify(
                repository => repository.AddAsync(
                    It.IsAny<Country>(),
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
            var repository = new Mock<ICountryRepository>();

            repository
                .Setup(repository => repository.ExistsByNameAsync(
                    "Brasil",
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var service = new CountryService(repository.Object);

            var dto = new CreateCountryDto("Brasil");

            await Assert.ThrowsAsync<ResourceConflictException>(
                () => service.CreateAsync(dto));

            repository.Verify(
                repository => repository.ExistsByNameAsync(
                    "Brasil",
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.AddAsync(
                    It.IsAny<Country>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCountry_WhenCountryExists()
        {
            var repository = new Mock<ICountryRepository>();

            var item = new Country
            {
                Id = 1,
                Name = "Brasil"
            };

            repository
                .Setup(repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            var service = new CountryService(repository.Object);

            var result = await service.GetByIdAsync(1);

            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Brasil", result.Name);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNull_WhenCountryDoesNotExist()
        {
            var repository = new Mock<ICountryRepository>();

            repository
                .Setup(repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Country?)null);

            var service = new CountryService(repository.Object);

            var result = await service.GetByIdAsync(999);

            Assert.Null(result);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdateCountry_WhenCountryExists()
        {
            var repository = new Mock<ICountryRepository>();

            var item = new Country
            {
                Id = 1,
                Name = "Brasil"
            };

            repository
                .Setup(repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            repository
                .Setup(repository => repository.ExistsByNameAsync(
                    "Brasil atualizado",
                    1,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            repository
                .Setup(repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new CountryService(repository.Object);

            var dto = new UpdateCountryDto("Brasil atualizado");

            var result = await service.UpdateAsync(1, dto);

            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Brasil atualizado", result.Name);

            Assert.Equal("Brasil atualizado", item.Name);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.ExistsByNameAsync(
                    "Brasil atualizado",
                    1,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNull_WhenCountryDoesNotExist()
        {
            var repository = new Mock<ICountryRepository>();

            repository
                .Setup(repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Country?)null);

            var service = new CountryService(repository.Object);

            var dto = new UpdateCountryDto("Brasil atualizado");

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
        public async Task DeleteAsync_ShouldDeleteCountry_WhenCountryExists()
        {
            var repository = new Mock<ICountryRepository>();

            var item = new Country
            {
                Id = 1,
                Name = "Brasil"
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

            var service = new CountryService(repository.Object);

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
        public async Task DeleteAsync_ShouldReturnFalse_WhenCountryDoesNotExist()
        {
            var repository = new Mock<ICountryRepository>();

            repository
                .Setup(repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Country?)null);

            var service = new CountryService(repository.Object);

            var result = await service.DeleteAsync(999);

            Assert.False(result);

            repository.Verify(
                repository => repository.GetByIdAsync(
                    999,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            repository.Verify(
                repository => repository.DeleteAsync(
                    It.IsAny<Country>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            repository.Verify(
                repository => repository.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
