using Citas.Domain.Common;

namespace Citas.Domain.Tests.Common;

public class ResultTests
{
    private static readonly Error Sample = new("Sample.Code", "Sample message");

    [Fact]
    public void Success_Created_IsSuccessWithoutError()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_Created_IsFailureWithError()
    {
        Result result = Result.Failure(Sample);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(Sample, result.Error);
    }

    [Fact]
    public void Failure_WithNoneError_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Failure(Error.None));
    }
}
