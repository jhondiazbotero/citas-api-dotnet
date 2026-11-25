using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Citas.Api.Http;

/// <summary>
/// Turns what the framework rejects before any use case runs (malformed JSON, a date with a zone, a missing or
/// unparsable query parameter) into problem details with a stable <c>code</c>. It never echoes the raw exception message.
/// </summary>
internal sealed class BadRequestExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public const string InvalidDateTimeCode = "Request.InvalidDateTime";
    public const string InvalidBodyCode = "Request.InvalidBody";
    public const string InvalidParameterCode = "Request.InvalidParameter";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException bad)
        {
            return false;
        }

        (string code, string detail) = Describe(bad);
        httpContext.Response.StatusCode = bad.StatusCode;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = bad.StatusCode,
                Detail = detail,
                Extensions = { [ProblemResult.CodeExtension] = code },
            },
        });
    }

    private static (string Code, string Detail) Describe(BadHttpRequestException bad)
    {
        for (Exception? current = bad; current is not null; current = current.InnerException)
        {
            if (current is InvalidClinicDateTimeException dateTime)
            {
                return (InvalidDateTimeCode, WithField(dateTime.Message, dateTime));
            }

            if (current is JsonException json)
            {
                return (InvalidBodyCode, WithField("The request body is not valid JSON or has a field with a wrong format.", json));
            }
        }

        return bad.Message.Contains("body", StringComparison.OrdinalIgnoreCase)
            ? (InvalidBodyCode, "The request body is missing or cannot be read.")
            : (InvalidParameterCode, "A route or query parameter is missing or has an invalid format.");
    }

    private static string WithField(string message, JsonException exception) =>
        exception.Path is { Length: > 0 } path ? $"{message} Field: {path}." : message;
}
