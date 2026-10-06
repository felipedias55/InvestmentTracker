using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Portfolios;
using Moq;

namespace InvestmentTracker.UnitTests.Portfolios
{
    public class QuoteBatchTests
    {
        [Theory]
        [InlineData("1.123456", "30.1234", "33.8423")]
        [InlineData("0.000001", "50", "0.0000")]
        [InlineData("0.000003", "50", "0.0002")]
        public void Value_UsesDecimalQuantityAndBankersRounding(string quantity, string price, string expected)
        {
            decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(Parse(expected), QuoteBatchService.CalculateValue(Parse(quantity), Parse(price)));
        }
        [Fact]
        public async Task InvalidOrDuplicatedRows_AreRejectedBeforePersistence()
        {
            var repository = new Mock<IQuoteBatchRepository>(MockBehavior.Strict);
            var service = new QuoteBatchService(repository.Object, TimeProvider.System);
            var row = new QuoteUpdate(1, "BRL", 10m, 2m, 15m, new(2026, 9, 1));
            foreach (var items in new IReadOnlyList<QuoteUpdate>[] { [], [row, row], [row with { UnitPrice = 0 }],
                [row with { UnitPrice = 1.12345m }], [row with { ExpectedQuantity = 0 }],
                [row with { ExpectedQuantity = 9999999999999m, UnitPrice = 999999999999999m }] })
                await Assert.ThrowsAsync<InputValidationException>(() => service.SaveAsync(1, new(Guid.NewGuid(), items), default));
            repository.VerifyNoOtherCalls();
        }
    }
}
