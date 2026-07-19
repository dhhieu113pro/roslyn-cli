using System.Text.Json;

namespace RoslynCli;

public static class OutputWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static bool IsSupportedFormat(string format) =>
        format.Equals("text", StringComparison.OrdinalIgnoreCase) ||
        format.Equals("json", StringComparison.OrdinalIgnoreCase);

    public static void WriteSearch(
        IReadOnlyList<SymbolSearchResult> results,
        string format,
        TextWriter writer)
    {
        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var envelope = new { schemaVersion = "1.0", count = results.Count, results };
            writer.WriteLine(JsonSerializer.Serialize(envelope, JsonOptions));
            return;
        }

        foreach (var result in results)
            writer.WriteLine($"{result.Kind,-8} {result.QualifiedName}  {result.File}:{result.Line}:{result.Column}");
    }
}
