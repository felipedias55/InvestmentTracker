namespace InvestmentTracker.Application.Common.Exceptions
{
    public class InputValidationException(string message, Exception? innerException = null)
        : Exception(message, innerException)
    {
    }
}
