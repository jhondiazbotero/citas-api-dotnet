using Citas.Domain.Errors;
using Microsoft.AspNetCore.Http;

namespace Citas.Api.Http;

internal static class ProblemDetailsSetup
{
    /// <summary>Makes sure every problem has a <c>code</c>. Domain and request errors already set theirs; the rest get one by status.</summary>
    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        if (problem.Extensions.ContainsKey(ProblemResult.CodeExtension))
        {
            return;
        }

        int status = problem.Status ?? context.HttpContext.Response.StatusCode;
        if (status == StatusCodes.Status415UnsupportedMediaType)
        {
            problem.Detail ??= "Send the body as application/json.";
        }

        problem.Extensions[ProblemResult.CodeExtension] = status switch
        {
            StatusCodes.Status401Unauthorized => AuthErrors.Unauthorized.Code,
            StatusCodes.Status403Forbidden => AuthErrors.Forbidden.Code,
            StatusCodes.Status404NotFound => "Http.NotFound",
            StatusCodes.Status405MethodNotAllowed => "Http.MethodNotAllowed",
            StatusCodes.Status415UnsupportedMediaType => "Request.UnsupportedMediaType",
            StatusCodes.Status500InternalServerError => "Server.UnexpectedError",
            _ => $"Http.{status}",
        };
    }
}
