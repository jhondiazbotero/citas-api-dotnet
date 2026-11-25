using System.Net;
using System.Net.Http.Json;
using Citas.Api.Tests.Support;
using Citas.Domain.Staff;
using Citas.Infrastructure.Persistence;
using Citas.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Citas.Api.Tests;

/// <summary>The first administrator of a real deployment comes from the environment, at startup.</summary>
[Collection(ApiCollection.Name)]
public class BootstrapAdminTests(PostgresFixture postgres)
{
    private const string Email = "dueno@clinica.test";
    private const string Password = "Una-clave-larga-1";

    private static Dictionary<string, string> Settings(string? email, string? password) => new()
    {
        ["Database:MigrateOnStartup"] = "true",
        ["Demo:SeedData"] = "false",

        // Always set, even to nothing: variables of the developer's machine must not leak into the test.
        ["Demo:UsersPassword"] = string.Empty,
        ["Bootstrap:AdminEmail"] = email ?? string.Empty,
        ["Bootstrap:AdminPassword"] = password ?? string.Empty,
    };

    private static async Task<List<StaffUser>> UsersAsync(CitasApiFactory factory)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CitasDbContext>().StaffUsers.ToListAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WithTheSettings_TheAdministratorExists_AndCanSignIn_AndARestartChangesNothing()
    {
        string connection = await postgres.CreateEmptyDatabaseAsync();
        try
        {
            await using (var first = new CitasApiFactory(connection, Settings(Email, Password)))
            {
                using HttpClient client = first.CreateClient();
                using var signIn = await client.PostAsJsonAsync("/api/v1/auth/token", new { email = Email, password = Password }, Json.Options, TestContext.Current.CancellationToken);

                Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
                Assert.Equal("Admin", (await signIn.ReadAsync()).GetProperty("role").GetString());
            }

            await using (var restarted = new CitasApiFactory(connection, Settings(Email, Password)))
            {
                restarted.CreateClient().Dispose();

                StaffUser only = Assert.Single(await UsersAsync(restarted));
                Assert.Equal(Email, only.Email);
            }
        }
        finally
        {
            await postgres.DropDatabaseAsync(connection);
        }
    }

    [Fact]
    public async Task WithoutTheSettings_NoUserIsCreated()
    {
        string connection = await postgres.CreateEmptyDatabaseAsync();
        try
        {
            await using var factory = new CitasApiFactory(connection, Settings(email: null, password: null));
            factory.CreateClient().Dispose();

            Assert.Empty(await UsersAsync(factory));
        }
        finally
        {
            await postgres.DropDatabaseAsync(connection);
        }
    }

    [Fact]
    public async Task AWeakPassword_StopsTheStartup_WithoutRevealingIt()
    {
        string connection = await postgres.CreateEmptyDatabaseAsync();
        try
        {
            await using var factory = new CitasApiFactory(connection, Settings(Email, "corta-11-ch"));

            var failure = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

            string text = failure.ToString();
            Assert.Contains("Bootstrap:AdminPassword", text, StringComparison.Ordinal);
            Assert.DoesNotContain("corta-11-ch", text, StringComparison.Ordinal);
        }
        finally
        {
            await postgres.DropDatabaseAsync(connection);
        }
    }
}
