using Citas.Domain.Common;
using FluentValidation.Results;

namespace Citas.Application.Common;

/// <summary>Turns FluentValidation output into the domain's <see cref="Error"/>.</summary>
internal static class ValidationErrors
{
    public const string Code = "Validation";

    /// <summary>The first failure as <c>Error("Validation", message)</c>.</summary>
    public static Error From(ValidationResult validation) =>
        new(Code, validation.Errors[0].ErrorMessage);
}
