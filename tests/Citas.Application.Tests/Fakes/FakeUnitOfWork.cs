using Citas.Application.Abstractions;
using Citas.Domain.Common;

namespace Citas.Application.Tests.Fakes;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    /// <summary>When set, SaveChangesAsync fails with this error (like the store rejecting an overlap).</summary>
    public Error? FailWith { get; set; }

    public int SaveCalls { get; private set; }

    public Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        return Task.FromResult(FailWith is null ? Result.Success() : Result.Failure(FailWith));
    }
}
