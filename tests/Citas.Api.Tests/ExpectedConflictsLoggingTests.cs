using Citas.Application.Abstractions;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Patients;
using Citas.Api.Tests.Support;
using Citas.Testing;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace Citas.Api.Tests;

/// <summary>
/// A 409 from a unique index is an expected answer, not a fault. EF Core would log the failed SQL command as an Error before the
/// answer leaves; the Serilog level for that category is set so the logs of a healthy API carry no Error for it.
/// </summary>
public class ExpectedConflictsLoggingTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private const string CommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";

    private readonly CapturingSink _sink = new();

    protected override CitasApiFactory CreateFactory(string connectionString) =>
        new(connectionString, configureServices: services => services.AddSingleton<ILogEventSink>(_sink));

    [Fact]
    public async Task ADuplicateRejectedByTheUniqueIndex_IsA409Failure_AndLogsNoErrorFromTheDatabaseCommands()
    {
        // The handlers look for a duplicate before inserting, so only a race reaches the index. Here the race is built by hand:
        // the second save happens after the first one committed, with a patient the second scope never saw.
        await SaveNewPatientAsync("1234567");

        Result result = await SaveNewPatientAsync("1234567");

        Assert.Equal(PatientErrors.DuplicateDocument, result.Error);
        Assert.DoesNotContain(_sink.Events, e => e.Level >= LogEventLevel.Error && SourceOf(e).StartsWith(CommandCategory, StringComparison.Ordinal));
    }

    private async Task<Result> SaveNewPatientAsync(string document)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IPatientRepository>().Add(Patient.Create("Luis Prueba", document).Value);
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Cancellation);
    }

    [Fact]
    public async Task TheSinkSeesTheLogsOfTheRequests_SoTheTestAboveProvesSomething()
    {
        await RegisterPatientAsync("7654321");

        Assert.Contains(_sink.Events, e => SourceOf(e).StartsWith("Serilog.AspNetCore", StringComparison.Ordinal));
    }

    private static string SourceOf(LogEvent logEvent) =>
        logEvent.Properties.TryGetValue("SourceContext", out LogEventPropertyValue? value) ? value.ToString().Trim('"') : string.Empty;

    private sealed class CapturingSink : ILogEventSink
    {
        private readonly List<LogEvent> _events = [];

        public IReadOnlyList<LogEvent> Events
        {
            get
            {
                lock (_events)
                {
                    return [.. _events];
                }
            }
        }

        public void Emit(LogEvent logEvent)
        {
            lock (_events)
            {
                _events.Add(logEvent);
            }
        }
    }
}
