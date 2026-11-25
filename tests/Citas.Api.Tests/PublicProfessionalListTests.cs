using System.Net;
using System.Text.Json;
using Citas.Api.Tests.Support;
using Citas.Testing;

namespace Citas.Api.Tests;

/// <summary>The public list is how a visitor finds the id to ask for free times. It shows the least it can.</summary>
public class PublicProfessionalListTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private async Task<JsonElement> ListAnonymouslyAsync()
    {
        using var response = await Anonymous.GetAsync($"{V1}/professionals", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync();
    }

    [Fact]
    public async Task Anonymous_GetsOnlyIdNameAndSpecialty_OfTheActiveOnes_ByName()
    {
        Guid zeta = await RegisterProfessionalAsync("Zeta Prueba");
        Guid alfa = await RegisterProfessionalAsync("Alfa Prueba");
        Guid retired = await RegisterProfessionalAsync("Beta Prueba");
        using (var deactivated = await PostAsync($"{V1}/professionals/{retired}/deactivate", new { }))
        {
            Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        }

        JsonElement list = await ListAnonymouslyAsync();

        JsonElement[] items = [.. list.EnumerateArray()];
        Assert.Equal([alfa, zeta], items.Select(i => i.GetProperty("id").GetGuid()));
        Assert.Equal(["Alfa Prueba", "Zeta Prueba"], items.Select(i => i.GetProperty("fullName").GetString()));
        Assert.All(items, item =>
        {
            Assert.Equal(["fullName", "id", "specialty"], item.EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal("Medicina general", item.GetProperty("specialty").GetString());
        });
    }

    [Fact]
    public async Task WithoutProfessionals_IsAnEmptyList()
    {
        JsonElement list = await ListAnonymouslyAsync();

        Assert.Equal(JsonValueKind.Array, list.ValueKind);
        Assert.Equal(0, list.GetArrayLength());
    }

    [Fact]
    public async Task TheIdOfTheList_WorksForTheFreeTimes()
    {
        Guid professional = await RegisterProfessionalAsync();
        JsonElement list = await ListAnonymouslyAsync();

        Guid listed = Assert.Single(list.EnumerateArray()).GetProperty("id").GetGuid();

        Assert.Equal(professional, listed);
        Assert.NotEmpty(await FreeStartsAsync(listed, day: 3));
    }
}
