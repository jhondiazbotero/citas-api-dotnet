using Citas.Domain.Errors;

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
        problem.Detail ??= status switch
        {
            StatusCodes.Status401Unauthorized => AuthErrors.Unauthorized.Message,
            StatusCodes.Status403Forbidden => AuthErrors.Forbidden.Message,
            StatusCodes.Status415UnsupportedMediaType => "Send the body as application/json.",
            _ => null,
        };

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
