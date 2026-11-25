using System.Net;
using System.Net.Http.Json;
using Citas.Api.Tests.Support;
using Citas.Domain.Staff;
using Citas.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Citas.Api.Tests;

[Collection(ApiCollection.Name)]
public class JwtConfigurationTests(PostgresFixture postgres)
{
    private async Task<(string Connection, Exception? Failure)> TryStartAsync(Dictionary<string, string> settings)
    {
        string connection = await postgres.CreateDatabaseAsync();
        await using var factory = new CitasApiFactory(connection, settings);
        Exception? failure = Record.Exception(() => factory.CreateClient().Dispose());
        await postgres.DropDatabaseAsync(connection);
        return (connection, failure);
    }

    private static string Messages(Exception? exception)
    {
        var all = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            all.Add(current.Message);
        }

        return string.Join(" | ", all);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WithoutASigningKey_TheAppDoesNotStart_AndSaysHowToFixIt(string key)
    {
        var (_, failure) = await TryStartAsync(new() { ["Jwt:SigningKey"] = key });

        Assert.NotNull(failure);
        string message = Messages(failure);
        Assert.Contains("Jwt:SigningKey", message, StringComparison.Ordinal);
        Assert.Contains("user-secrets", message, StringComparison.Ordinal);
        Assert.Contains("Jwt__SigningKey", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithAShortSigningKey_TheAppDoesNotStart_AndDoesNotPrintTheKey()
    {
        string shortKey = new('k', 31);

        var (_, failure) = await TryStartAsync(new() { ["Jwt:SigningKey"] = shortKey });

        Assert.NotNull(failure);
        string message = Messages(failure);
        Assert.Contains("too short", message, StringComparison.Ordinal);
        Assert.Contains("31 bytes", message, StringComparison.Ordinal);
        Assert.DoesNotContain(shortKey, message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithAKeyOfExactly32Bytes_TheAppStarts()
    {
        var (_, failure) = await TryStartAsync(new() { ["Jwt:SigningKey"] = new string('k', 32) });

        Assert.Null(failure);
    }

    [Fact]
    public async Task TheLengthIsMeasuredInBytes_NotInCharacters()
    {
        // 16 characters of 2 bytes each = 32 bytes: accepted. 15 of them = 30 bytes: refused.
        var (_, enough) = await TryStartAsync(new() { ["Jwt:SigningKey"] = new string('ñ', 16) });
        var (_, notEnough) = await TryStartAsync(new() { ["Jwt:SigningKey"] = new string('ñ', 15) });

        Assert.Null(enough);
        Assert.NotNull(notEnough);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1441")]
    public async Task AnAbsurdLifetime_TheAppDoesNotStart(string minutes)
    {
        var (_, failure) = await TryStartAsync(new() { ["Jwt:ExpiresMinutes"] = minutes });

        Assert.Contains("Jwt:ExpiresMinutes", Messages(failure), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Jwt:Issuer")]
    [InlineData("Jwt:Audience")]
    public async Task WithoutIssuerOrAudience_TheAppDoesNotStart(string key)
    {
        var (_, failure) = await TryStartAsync(new() { [key] = "" });

        Assert.Contains(key, Messages(failure), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLifetimeAndTheNamesComeFromConfiguration()
    {
        string connection = await postgres.CreateDatabaseAsync();
        await using var factory = new CitasApiFactory(connection, new Dictionary<string, string>
        {
            ["Jwt:ExpiresMinutes"] = "5",
            ["Jwt:Issuer"] = "mi-emisor",
            ["Jwt:Audience"] = "mi-audiencia",
        });
        await factory.AddUserAsync("recepcion@tests.local", "Clave-de-pruebas-1", StaffRole.Reception);
        using HttpClient client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/token", new { email = "recepcion@tests.local", password = "Clave-de-pruebas-1" }, Json.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonWebToken token = new JsonWebTokenHandler().ReadJsonWebToken((await response.ReadAsync()).GetProperty("accessToken").GetString()!);
        Assert.Equal(CitasApiFactory.StartOfTime.AddMinutes(5), new DateTimeOffset(token.ValidTo, TimeSpan.Zero));
        Assert.Equal("mi-emisor", token.Issuer);
        Assert.Contains("mi-audiencia", token.Audiences);
        await postgres.DropDatabaseAsync(connection);
    }
}
