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

    public static void WriteReferences(IReadOnlyList<SymbolReferenceResult> results, string format, TextWriter writer) =>
        WriteList(results, format, writer, result =>
            $"{(result.IsDefinition ? "definition" : "reference"),-10} {result.Symbol}  {result.File}:{result.Line}:{result.Column}");

    public static void WriteSymbolInfo(IReadOnlyList<SymbolInfoResult> results, string format, TextWriter writer) =>
        WriteList(results, format, writer, result =>
            $"{result.Kind,-8} {result.QualifiedName} [{result.Accessibility}]  {result.File}:{result.Line}:{result.Column}");

    public static void WriteDependencies(DependencyAnalysisResult result, string format, TextWriter writer)
    {
        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(new { schemaVersion = "1.0", result.Dependencies, result.NamespaceUsages }, writer);
            return;
        }
        foreach (var dependency in result.Dependencies)
            writer.WriteLine($"{dependency.Type,-8} {dependency.Project} -> {dependency.Dependency}");
        foreach (var usage in result.NamespaceUsages)
            writer.WriteLine($"using    {usage.Project} -> {usage.Namespace} ({usage.Count})");
    }

    public static void WriteComplexity(IReadOnlyList<ComplexityResult> results, string format, TextWriter writer) =>
        WriteList(results, format, writer, result =>
            $"{result.Complexity,3} {result.QualifiedName}  {result.File}:{result.Line}");

    public static void WriteDiagnostics(IReadOnlyList<DiagnosticResult> results, string format, TextWriter writer) =>
        WriteList(results, format, writer, result =>
            $"{result.Severity,-7} {result.Id} [{result.Source}] {result.Message}  {result.File}:{result.Line}:{result.Column}");

    private static void WriteList<T>(IReadOnlyList<T> results, string format, TextWriter writer, Func<T, string> formatText)
    {
        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(new { schemaVersion = "1.0", count = results.Count, results }, writer);
            return;
        }
        foreach (var result in results) writer.WriteLine(formatText(result));
    }

    private static void WriteJson<T>(T value, TextWriter writer) =>
        writer.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    public static void WriteRawJson(string json, string format, TextWriter writer)
    {
        using var document = JsonDocument.Parse(json);
        writer.WriteLine(JsonSerializer.Serialize(document.RootElement, JsonOptions));
    }
}
