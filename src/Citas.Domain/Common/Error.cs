namespace Citas.Domain.Common;

/// <summary>A business error: a stable machine-readable code plus a short message.</summary>
public sealed record Error(string Code, string Message)
{
    /// <summary>The absence of an error. Only carried by successful results.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);
}
