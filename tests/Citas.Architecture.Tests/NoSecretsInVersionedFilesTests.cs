using System.Text.Json;

namespace Citas.Architecture.Tests;

/// <summary>Settings files travel with the repository, so they must not hold a password or a key: those come from user-secrets or the environment.</summary>
public class NoSecretsInVersionedFilesTests
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

    private static string[] SettingsFiles() =>
        Directory.GetFiles(Path.Combine(RepositoryRoot(), "src"), "appsettings*.json", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

    private static IEnumerable<(string Path, string Value)> Values(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    foreach (var value in Values(property.Value, $"{path}:{property.Name}"))
                    {
                        yield return value;
                    }
                }

                break;
            case JsonValueKind.String:
                yield return (path, element.GetString()!);
                break;
        }
    }

    [Fact]
    public void ThereAreSettingsFilesToCheck() => Assert.NotEmpty(SettingsFiles());

    [Fact]
    public void NoSettingsFile_HasAPasswordInAConnectionString()
    {
        foreach (string file in SettingsFiles())
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var (path, value) in Values(document.RootElement, string.Empty))
            {
                Assert.False(
                    value.Contains("Password=", StringComparison.OrdinalIgnoreCase) || value.Contains("Pwd=", StringComparison.OrdinalIgnoreCase),
                    $"{Path.GetFileName(file)} holds a password in '{path}'. Use user-secrets or the environment.");
            }
        }
    }

    [Fact]
    public void NoSettingsFile_HasASigningKeyOrAPasswordValue()
    {
        string[] sensitive = ["SigningKey", "Password", "Secret"];
        foreach (string file in SettingsFiles())
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var (path, value) in Values(document.RootElement, string.Empty))
            {
                bool looksSensitive = sensitive.Any(word => path.Contains(word, StringComparison.OrdinalIgnoreCase));
                Assert.False(looksSensitive && value.Length > 0, $"{Path.GetFileName(file)} has a value in '{path}'. Use user-secrets or the environment.");
            }
        }
    }
}
