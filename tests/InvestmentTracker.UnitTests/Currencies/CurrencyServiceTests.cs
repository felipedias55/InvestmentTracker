using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Currencies.Dtos;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.Currencies.Services;
using InvestmentTracker.Domain.Entities;
using Moq;

namespace InvestmentTracker.UnitTests.Currencies
{
    public class CurrencyServiceTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("BR")]
        [InlineData("BRLL")]
        [InlineData("B1L")]
        [InlineData("BŔL")]
        public async Task CreateAndUpdate_ShouldRejectInvalidCode(string? code)
        {
            var repository = new Mock<ICurrencyRepository>(MockBehavior.Strict);
            var service = new CurrencyService(repository.Object);
            await Assert.ThrowsAsync<InputValidationException>(() => service.CreateAsync(new(code!, "Real")));
            await Assert.ThrowsAsync<InputValidationException>(() => service.UpdateAsync(1, new(code!, "Real")));
            repository.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task Create_ShouldRejectMissingName(string? name)
        {
            var service = new CurrencyService(new Mock<ICurrencyRepository>(MockBehavior.Strict).Object);
            await Assert.ThrowsAsync<InputValidationException>(() => service.CreateAsync(new("BRL", name!)));
        }

        [Fact]
        public async Task CreateAndUpdate_ShouldEnforceFieldLengths()
        {
            var service = new CurrencyService(new Mock<ICurrencyRepository>(MockBehavior.Strict).Object);
            await Assert.ThrowsAsync<InputValidationException>(() => service.CreateAsync(new("BRL", new string('a', 101))));
            await Assert.ThrowsAsync<InputValidationException>(() => service.UpdateAsync(1, new("BRL", "Real", new string('a', 11))));
        }

        [Fact]
        public async Task Create_ShouldNormalizeCodeAndOptionalSymbol()
        {
            var repository = new Mock<ICurrencyRepository>();
            var service = new CurrencyService(repository.Object);
            var result = await service.CreateAsync(new(" brl ", " Real ", "   "));
            Assert.Equal("BRL", result.Code);
            Assert.Equal("Real", result.Name);
            Assert.Null(result.Symbol);
            repository.Verify(r => r.ExistsByCodeAsync("BRL", null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Update_ShouldExcludeItselfFromDuplicateCheck()
        {
            var repository = new Mock<ICurrencyRepository>();
            repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Currency { Id = 1, Code = "BRL", Name = "Real" });
            var result = await new CurrencyService(repository.Object).UpdateAsync(1, new(" brl ", "Real brasileiro", " R$ "));
            Assert.Equal("R$", result!.Symbol);
            repository.Verify(r => r.ExistsByCodeAsync("BRL", 1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Create_ShouldRejectDuplicateCode()
        {
            var repository = new Mock<ICurrencyRepository>();
            repository.Setup(r => r.ExistsByCodeAsync("BRL", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            await Assert.ThrowsAsync<ResourceConflictException>(() => new CurrencyService(repository.Object).CreateAsync(new("brl", "Real")));
            repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
