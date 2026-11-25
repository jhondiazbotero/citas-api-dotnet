using Citas.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Citas.Api.Http;

/// <summary>Writes a domain error as RFC 9457 problem details through <see cref="IProblemDetailsService"/>, so every error looks the same.</summary>
internal sealed class ProblemResult(Error error) : IResult
{
    public const string CodeExtension = "code";

    public Task ExecuteAsync(HttpContext httpContext)
    {
        int status = ErrorStatus.For(error);
        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Detail = error.Message,
            Extensions = { [CodeExtension] = error.Code },
        };

        return httpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem })
            .AsTask();
    }
}
