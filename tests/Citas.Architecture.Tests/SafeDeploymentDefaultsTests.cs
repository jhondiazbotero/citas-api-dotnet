namespace Citas.Architecture.Tests;

/// <summary>
/// <c>X-Forwarded-For</c> is only believable behind a proxy that writes it. The images and the compose file must not turn the
/// header on by themselves: whoever puts a proxy in front sets <c>ForwardedHeaders__Enabled=true</c> on purpose.
/// </summary>
public class SafeDeploymentDefaultsTests
{
    private static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CitasApi.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("CitasApi.slnx was not found above the test output folder.");
    }

    private static string[] CodeLines(string fileName) =>
        [.. File.ReadAllLines(Path.Combine(RepositoryRoot(), fileName))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))];

    [Theory]
    [InlineData("Dockerfile")]
    [InlineData("docker-compose.yml")]
    public void TheFile_DoesNotEnableForwardedHeaders(string fileName)
    {
        string[] lines = CodeLines(fileName);

        Assert.NotEmpty(lines);
        Assert.DoesNotContain(lines, line => line.Contains("ForwardedHeaders__Enabled", StringComparison.OrdinalIgnoreCase));
    }
}
