using System.Collections.Concurrent;

namespace InvestmentTracker.Infrastructure.ExchangeRates
{
    public sealed class ExchangeRateRefreshCoordinator
    {
        private readonly ConcurrentDictionary<string, RefreshState> _states = new();
        public RefreshState For(string pair) => _states.GetOrAdd(pair, _ => new RefreshState());

        public sealed class RefreshState
        {
            public SemaphoreSlim Gate { get; } = new(1, 1);
            public DateTimeOffset RetryAfter { get; set; }
        }
    }
}
