using Citas.Api.Setup;
using Citas.Application;
using Citas.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddApiLogging();
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration);

var app = builder.Build();

await app.RunStartupTasksAsync();
app.UseApi();
app.MapApi();

await app.RunAsync();

// Exposed so WebApplicationFactory<Program> can start the API in tests.
public partial class Program;
