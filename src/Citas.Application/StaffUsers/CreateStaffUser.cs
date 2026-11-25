using Citas.Application.Abstractions;
using Citas.Application.Common;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Professionals;
using Citas.Domain.Staff;
using FluentValidation;

namespace Citas.Application.StaffUsers;

/// <summary>A new staff user. The password arrives in plain text and leaves as a hash.</summary>
public sealed record CreateStaffUserCommand(string Email, string Password, StaffRole Role, Guid? ProfessionalId);

/// <summary>What an administrator sees of a user they created. The password hash never leaves the server.</summary>
public sealed record StaffUserResponse(Guid Id, string Email, StaffRole Role, Guid? ProfessionalId, bool IsActive);

public sealed class CreateStaffUserValidator : AbstractValidator<CreateStaffUserCommand>
{
    public const int MinPasswordLength = 12;
    public const int MaxPasswordLength = 256;

    public CreateStaffUserValidator()
    {
        RuleFor(c => c.Email).NotEmpty().WithMessage("Email is required.");
        RuleFor(c => c.Password).NotEmpty().WithMessage("Password is required.");
        RuleFor(c => c.Password)
            .Must(IsAcceptablePassword)
            .WithMessage($"Password must be {MinPasswordLength} to {MaxPasswordLength} characters and not only spaces.");
    }

    /// <summary>The one password rule of the staff users, shared with the first administrator created at startup.</summary>
    public static bool IsAcceptablePassword(string? password) =>
        password is { Length: >= MinPasswordLength and <= MaxPasswordLength } && !string.IsNullOrWhiteSpace(password);
}

/// <summary>
/// Creates a Reception or Professional user. Administrators are not created here: the first one comes from the startup
/// configuration. A Professional user must point at a professional that exists and is active.
/// </summary>
public sealed class CreateStaffUserHandler(
    IValidator<CreateStaffUserCommand> validator,
    IStaffUserRepository users,
    IProfessionalRepository professionals,
    IPasswordHasher hasher,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<StaffUserResponse>> HandleAsync(CreateStaffUserCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrors.From(validation);
        }

        if (command.Role is not (StaffRole.Reception or StaffRole.Professional))
        {
            return StaffUserErrors.RoleNotAllowed;
        }

        if (command is { Role: StaffRole.Professional, ProfessionalId: { } professionalId } && professionalId != Guid.Empty)
        {
            Professional? professional = await professionals.GetByIdAsync(professionalId, cancellationToken);
            if (professional is null)
            {
                return ProfessionalErrors.NotFound;
            }

            if (!professional.IsActive)
            {
                return ProfessionalErrors.Inactive;
            }
        }

        // The slow hash is only worth computing for a request that can still succeed; the shape of email, role and link is
        // checked by the domain once there is a hash to give it.
        if (await users.GetByEmailAsync(StaffUser.NormalizeEmail(command.Email), cancellationToken) is not null)
        {
            return StaffUserErrors.DuplicateEmail;
        }

        Result<StaffUser> created = StaffUser.Create(command.Email, hasher.Hash(command.Password), command.Role, command.ProfessionalId);
        if (created.IsFailure)
        {
            return created.Error;
        }

        StaffUser user = created.Value;
        users.Add(user);
        Result saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        return new StaffUserResponse(user.Id, user.Email, user.Role, user.ProfessionalId, user.IsActive);
    }
}
