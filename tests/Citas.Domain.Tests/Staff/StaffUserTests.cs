using Citas.Domain.Errors;
using Citas.Domain.Staff;

namespace Citas.Domain.Tests.Staff;

public class StaffUserTests
{
    private const string Hash = "AQAAAAIAAYagAAAAEExampleHash";
    private static readonly Guid ProfessionalId = Guid.NewGuid();

    [Fact]
    public void Create_Admin_Succeeds_Active_WithoutProfessional()
    {
        var result = StaffUser.Create("admin@demo.local", Hash, StaffRole.Admin);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("admin@demo.local", result.Value.Email);
        Assert.Equal(Hash, result.Value.PasswordHash);
        Assert.Equal(StaffRole.Admin, result.Value.Role);
        Assert.Null(result.Value.ProfessionalId);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public void Create_Reception_Succeeds()
    {
        Assert.True(StaffUser.Create("recepcion@demo.local", Hash, StaffRole.Reception).IsSuccess);
    }

    [Fact]
    public void Create_Professional_WithProfessionalId_KeepsTheLink()
    {
        var result = StaffUser.Create("marquina@demo.local", Hash, StaffRole.Professional, ProfessionalId);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProfessionalId, result.Value.ProfessionalId);
    }

    [Fact]
    public void Create_Professional_WithoutProfessionalId_Fails()
    {
        var result = StaffUser.Create("marquina@demo.local", Hash, StaffRole.Professional);

        Assert.Equal(StaffUserErrors.ProfessionalRequired, result.Error);
    }

    [Fact]
    public void Create_Professional_WithEmptyGuid_Fails()
    {
        var result = StaffUser.Create("marquina@demo.local", Hash, StaffRole.Professional, Guid.Empty);

        Assert.Equal(StaffUserErrors.ProfessionalRequired, result.Error);
    }

    [Theory]
    [InlineData(StaffRole.Admin)]
    [InlineData(StaffRole.Reception)]
    public void Create_NonProfessionalRole_WithProfessionalId_Fails(StaffRole role)
    {
        var result = StaffUser.Create("alguien@demo.local", Hash, role, ProfessionalId);

        Assert.Equal(StaffUserErrors.ProfessionalNotAllowed, result.Error);
    }

    [Fact]
    public void Create_UndefinedRole_Fails()
    {
        var result = StaffUser.Create("alguien@demo.local", Hash, (StaffRole)99);

        Assert.Equal(StaffUserErrors.InvalidRole, result.Error);
    }

    [Theory]
    [InlineData("Admin@Demo.Local", "admin@demo.local")]
    [InlineData("  admin@demo.local  ", "admin@demo.local")]
    [InlineData("ADMIN@DEMO.LOCAL", "admin@demo.local")]
    public void Create_NormalizesTheEmail(string input, string expected)
    {
        var result = StaffUser.Create(input, Hash, StaffRole.Admin);

        Assert.Equal(expected, result.Value.Email);
        Assert.Equal(expected, StaffUser.NormalizeEmail(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sin-arroba")]
    [InlineData("@demo.local")]
    [InlineData("admin@")]
    [InlineData("admin@demo")]
    [InlineData("dos@@demo.local")]
    [InlineData("con espacio@demo.local")]
    [InlineData("a@b@demo.local")]
    public void Create_InvalidEmail_Fails(string? email)
    {
        var result = StaffUser.Create(email, Hash, StaffRole.Admin);

        Assert.Equal(StaffUserErrors.InvalidEmail, result.Error);
    }

    [Fact]
    public void Create_EmailLongerThan254_Fails()
    {
        string email = new string('a', 250) + "@x.co";

        Assert.Equal(StaffUserErrors.InvalidEmail, StaffUser.Create(email, Hash, StaffRole.Admin).Error);
    }

    [Fact]
    public void Create_EmailOfExactly254_Succeeds()
    {
        string email = new string('a', 246) + "@demo.co"; // 246 + 8 = 254

        Assert.Equal(254, email.Length);
        Assert.True(StaffUser.Create(email, Hash, StaffRole.Admin).IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutPasswordHash_Fails(string? hash)
    {
        var result = StaffUser.Create("admin@demo.local", hash, StaffRole.Admin);

        Assert.Equal(StaffUserErrors.InvalidPasswordHash, result.Error);
    }

    [Fact]
    public void Create_TwoUsers_GetDifferentIds()
    {
        var a = StaffUser.Create("a@demo.local", Hash, StaffRole.Admin).Value;
        var b = StaffUser.Create("b@demo.local", Hash, StaffRole.Admin).Value;

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Deactivate_MakesTheUserInactive_AndRepeatingIsHarmless()
    {
        var user = StaffUser.Create("a@demo.local", Hash, StaffRole.Admin).Value;

        user.Deactivate();
        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NormalizeEmail_BlankInput_GivesAnEmptyString(string? input)
    {
        Assert.Equal(string.Empty, StaffUser.NormalizeEmail(input));
    }
}
