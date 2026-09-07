using InvestmentTracker.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Exceptions
{
    public sealed class ApplicationExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var status = exception switch
            {
                InputValidationException => StatusCodes.Status400BadRequest,
                ResourceConflictException => StatusCodes.Status409Conflict,
                _ => (int?)null
            };

            if (status is null)
            {
                return false;
            }

            httpContext.Response.StatusCode = status.Value;
            var problem = new ProblemDetails
            {
                Status = status.Value,
                Title = status == 400 ? "Dados inválidos." : "Conflito na operação.",
                Detail = exception.Message,
                Instance = httpContext.Request.Path
            };
            // Preserve the existing error message contract for API consumers.
            problem.Extensions["message"] = exception.Message;
            await httpContext.Response.WriteAsJsonAsync(
                problem, options: null, contentType: "application/problem+json",
                cancellationToken: cancellationToken);
            return true;
        }
    }
}
