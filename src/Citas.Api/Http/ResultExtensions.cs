using Citas.Domain.Common;

namespace Citas.Api.Http;

/// <summary>The single place where a use case <see cref="Result"/> becomes an HTTP response.</summary>
internal static class ResultExtensions
{
    /// <summary>200 with the value, or the problem details of the error.</summary>
    public static IResult ToOk<T>(this Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : new ProblemResult(result.Error);

    /// <summary>204 without body, or the problem details of the error.</summary>
    public static IResult ToNoContent(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : new ProblemResult(result.Error);

    /// <summary>201 with the value and a <c>Location</c> header pointing at the named route, or the problem details of the error.</summary>
    public static IResult ToCreated<T>(this Result<T> result, string routeName, Func<T, object> routeValues) =>
        result.IsSuccess
            ? TypedResults.CreatedAtRoute(result.Value, routeName, routeValues(result.Value))
            : new ProblemResult(result.Error);
}
