using Citas.Api.Security;

namespace Citas.Api.Tests;

public class JwtOptionsTests
{
    private const string Key = "llave-de-prueba-que-no-debe-salir-en-ningun-texto-0123456789";

    private static readonly JwtOptions Options = new("citas-api", "citas-clients", Key, 60);

    [Fact]
    public void ToString_NeverContainsTheSigningKey()
    {
        Assert.DoesNotContain(Key, Options.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("SigningKey", Options.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_StillShowsTheSettingsThatAreNotSecret()
    {
        string text = Options.ToString();

        Assert.Contains("citas-api", text, StringComparison.Ordinal);
        Assert.Contains("citas-clients", text, StringComparison.Ordinal);
        Assert.Contains("60", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AStringInterpolationOrAFormat_DoesNotLeakTheKeyEither()
    {
        Assert.DoesNotContain(Key, $"{Options}", StringComparison.Ordinal);
        Assert.DoesNotContain(Key, string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", Options), StringComparison.Ordinal);
    }

    [Fact]
    public void TheKeyIsStillAvailableToTheCodeThatSignsTokens()
    {
        Assert.Equal(Key, Options.SigningKey);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(Key), Options.KeyBytes);
    }

    [Fact]
    public void TwoOptionsWithTheSameValues_AreEqual_AndADifferentKeyMakesThemDiffer()
    {
        Assert.Equal(Options, new JwtOptions("citas-api", "citas-clients", Key, 60));
        Assert.NotEqual(Options, new JwtOptions("citas-api", "citas-clients", Key + "x", 60));
    }
}
