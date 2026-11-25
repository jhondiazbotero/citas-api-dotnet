var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();

// Exposed so WebApplicationFactory<Program> can start the API in tests.
public partial class Program;
