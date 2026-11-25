using System.Net;
using System.Net.Http.Json;
using Citas.Api.Tests.Support;
using Citas.Infrastructure.Persistence;
using Citas.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Citas.Api.Tests;

/// <summary>The real startup path: migrations, demo data and demo users, then signing in through HTTP.</summary>
[Collection(ApiCollection.Name)]
public class DemoUsersTests(PostgresFixture postgres)
{
    private const string DemoPassword = "Clave-de-demo-1";

    private async Task<(CitasApiFactory Factory, string Connection)> StartAsync(string? usersPassword)
    {
        string connection = await postgres.CreateEmptyDatabaseAsync();
        var settings = new Dictionary<string, string> { ["Database:MigrateOnStartup"] = "true", ["Demo:SeedData"] = "true" };
        if (usersPassword is not null)
        {
            settings["Demo:UsersPassword"] = usersPassword;
        }

        var factory = new CitasApiFactory(connection, settings);
        factory.CreateClient().Dispose();
        return (factory, connection);
    }

    [Fact]
    public async Task WithAPassword_TheDemoUsersCanSignIn_AndAProfessionalGetsTheirOwnId()
    {
        var (factory, connection) = await StartAsync(DemoPassword);
        await using (factory)
        {
            using HttpClient client = factory.CreateClient();

            using var marquina = await client.PostAsJsonAsync("/api/v1/auth/token", new { email = "marquina@demo.local", password = DemoPassword }, Json.Options, TestContext.Current.CancellationToken);
            using var admin = await client.PostAsJsonAsync("/api/v1/auth/token", new { email = "admin@demo.local", password = DemoPassword }, Json.Options, TestContext.Current.CancellationToken);
            using var wrong = await client.PostAsJsonAsync("/api/v1/auth/token", new { email = "admin@demo.local", password = "otra" }, Json.Options, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, marquina.StatusCode);
            Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CitasDbContext>();
            Guid expected = (await db.Professionals.SingleAsync(p => p.FullName == "Dra. Elena Marquina", TestContext.Current.CancellationToken)).Id;
            JsonWebToken token = new JsonWebTokenHandler().ReadJsonWebToken((await marquina.ReadAsync()).GetProperty("accessToken").GetString()!);
            Assert.Equal(expected.ToString(), token.GetClaim("professional_id").Value);
            Assert.Equal(5, await db.StaffUsers.CountAsync(TestContext.Current.CancellationToken));
        }

        await postgres.DropDatabaseAsync(connection);
    }

    [Fact]
    public async Task WithoutAPassword_NoUsersExist_AndNobodyCanSignIn()
    {
        var (factory, connection) = await StartAsync(usersPassword: null);
        await using (factory)
        {
            using HttpClient client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync("/api/v1/auth/token", new { email = "admin@demo.local", password = DemoPassword }, Json.Options, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<CitasDbContext>().StaffUsers.CountAsync(TestContext.Current.CancellationToken));
        }

        await postgres.DropDatabaseAsync(connection);
    }
}
