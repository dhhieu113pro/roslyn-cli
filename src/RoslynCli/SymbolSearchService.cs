using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.Text.RegularExpressions;

namespace RoslynCli;

public sealed record SymbolSearchResult(
    string Name,
    string QualifiedName,
    string Kind,
    string Project,
    string File,
    int Line,
    int Column);

public static class SymbolSearchService
{
    private static readonly SymbolDisplayFormat QualifiedNameFormat =
        SymbolDisplayFormat.CSharpErrorMessageFormat;

    public static async Task<IReadOnlyList<SymbolSearchResult>> SearchPathAsync(
        string path,
        string pattern,
        string? kind,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Search pattern cannot be empty.", nameof(pattern));

        var normalizedKind = NormalizeKind(kind);
        EnsureMSBuildRegistered();

        using var workspace = MSBuildWorkspace.Create();
        var extension = Path.GetExtension(path).ToLowerInvariant();
        Solution solution = extension switch
        {
            ".csproj" => (await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken)).Solution,
            ".sln" or ".slnx" => await workspace.OpenSolutionAsync(path, cancellationToken: cancellationToken),
            _ => throw new ArgumentException("Path must point to a .sln, .slnx, or .csproj file.", nameof(path))
        };

        return await SearchSolutionAsync(solution, pattern, normalizedKind, limit, cancellationToken);
    }

    public static async Task<IReadOnlyList<SymbolSearchResult>> SearchSolutionAsync(
        Solution solution,
        string pattern,
        string? kind,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1)
            throw new ArgumentOutOfRangeException(nameof(limit));

        var normalizedKind = NormalizeKind(kind);
        var results = new List<SymbolSearchResult>();

        foreach (var project in solution.Projects
                     .Where(project => project.Language == LanguageNames.CSharp)
                     .OrderBy(project => project.Name, StringComparer.Ordinal))
        {
            var compilation = (await project.GetCompilationAsync(cancellationToken))!;

            VisitNamespace(compilation.Assembly.GlobalNamespace, project.Name, pattern, normalizedKind, results);
            if (results.Count >= limit)
                break;
        }

        return results
            .OrderBy(result => result.QualifiedName, StringComparer.Ordinal)
            .ThenBy(result => result.File, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    private static void VisitNamespace(
        INamespaceSymbol namespaceSymbol,
        string projectName,
        string pattern,
        string? kind,
        List<SymbolSearchResult> results)
    {
        foreach (var member in namespaceSymbol.GetMembers().OrderBy(member => member.Name, StringComparer.Ordinal))
        {
            if (member is INamespaceSymbol childNamespace)
                VisitNamespace(childNamespace, projectName, pattern, kind, results);
            else if (member is INamedTypeSymbol type)
                VisitType(type, projectName, pattern, kind, results);
        }
    }

    private static void VisitType(
        INamedTypeSymbol type,
        string projectName,
        string pattern,
        string? kind,
        List<SymbolSearchResult> results)
    {
        AddIfMatch(type, projectName, pattern, kind, results);

        foreach (var member in type.GetMembers().OrderBy(member => member.Name, StringComparer.Ordinal))
        {
            if (member is INamedTypeSymbol nestedType)
                VisitType(nestedType, projectName, pattern, kind, results);
            else
                AddIfMatch(member, projectName, pattern, kind, results);
        }
    }

    private static void AddIfMatch(
        ISymbol symbol,
        string projectName,
        string pattern,
        string? kind,
        List<SymbolSearchResult> results)
    {
        if (!MatchesPattern(symbol.Name, pattern) &&
            (!HasWildcards(pattern) || !MatchesPattern(symbol.ToDisplayString(QualifiedNameFormat), pattern)))
            return;

        var resultKind = GetKind(symbol);
        if (kind is not null && !string.Equals(kind, resultKind, StringComparison.Ordinal))
            return;

        foreach (var location in symbol.Locations.Where(location => location.IsInSource))
        {
            var span = location.GetLineSpan();
            results.Add(new SymbolSearchResult(
                symbol.Name,
                symbol.ToDisplayString(QualifiedNameFormat),
                resultKind,
                projectName,
                span.Path,
                span.StartLinePosition.Line + 1,
                span.StartLinePosition.Character + 1));
        }
    }

    private static string GetKind(ISymbol symbol) =>
        symbol is INamedTypeSymbol ? "type" : symbol.Kind.ToString().ToLowerInvariant();

    private static bool MatchesPattern(string value, string pattern)
    {
        if (!HasWildcards(pattern))
            return value.Contains(pattern, StringComparison.OrdinalIgnoreCase);

        var regex = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return Regex.IsMatch(value, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool HasWildcards(string pattern) => pattern.Contains('*') || pattern.Contains('?');

    private static string? NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
            return null;

        var normalized = kind.ToLowerInvariant();
        if (normalized is not ("type" or "method" or "property" or "field" or "event"))
            throw new ArgumentException("Kind must be type, method, property, field, or event.", nameof(kind));

        return normalized;
    }

    private static void EnsureMSBuildRegistered()
    {
        MSBuildRegistration.Ensure();
    }
}
