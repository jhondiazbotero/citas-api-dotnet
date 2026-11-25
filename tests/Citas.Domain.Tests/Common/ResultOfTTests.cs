using Citas.Domain.Common;

namespace Citas.Domain.Tests.Common;

public class ResultOfTTests
{
    private static readonly Error Sample = new("Sample.Code", "Sample message");

    [Fact]
    public void Success_WithValue_ExposesValue()
    {
        Result<int> result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Value_OnFailure_ThrowsInvalidOperation()
    {
        Result<int> result = Result.Failure<int>(Sample);

        Assert.True(result.IsFailure);
        Assert.Equal(Sample, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromValue_IsSuccess()
    {
        Result<string> result = "hello";

        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromError_IsFailure()
    {
        Result<string> result = Sample;

        Assert.True(result.IsFailure);
        Assert.Equal(Sample, result.Error);
    }

    [Fact]
    public void Failure_WithNoneError_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Failure<int>(Error.None));
    }
}
