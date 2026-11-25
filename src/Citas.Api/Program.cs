var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();

// Expuesta para que WebApplicationFactory<Program> pueda arrancar la API en las pruebas.
public partial class Program;
