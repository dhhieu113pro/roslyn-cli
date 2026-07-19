using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace RoslynCli;

public sealed record SymbolReferenceResult(
    string Symbol,
    string Project,
    string File,
    int Line,
    int Column,
    bool IsDefinition);

public sealed record SymbolInfoResult(
    string Name,
    string QualifiedName,
    string Kind,
    string Accessibility,
    string Namespace,
    string? ContainingType,
    string? ReturnType,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<string> Attributes,
    string? Documentation,
    string File,
    int Line,
    int Column);

public sealed record ProjectDependencyResult(string Project, string Dependency, string Type);
public sealed record NamespaceUsageResult(string Project, string Namespace, int Count);
public sealed record DependencyAnalysisResult(
    IReadOnlyList<ProjectDependencyResult> Dependencies,
    IReadOnlyList<NamespaceUsageResult> NamespaceUsages);

public sealed record ComplexityResult(
    string Project,
    string QualifiedName,
    string File,
    int Line,
    int Complexity);

public sealed record DiagnosticResult(string Id, string Severity, string Message, string File, int Line, int Column, string Source);

public static class RoslynAnalysisService
{
    private static readonly SymbolDisplayFormat QualifiedNameFormat = SymbolDisplayFormat.CSharpErrorMessageFormat;

    public static async Task<IReadOnlyList<SymbolReferenceResult>> FindReferencesPathAsync(
        string path, string symbolName, bool includeDefinition, CancellationToken cancellationToken = default)
    {
        using var workspace = MSBuildWorkspace.Create();
        var solution = await OpenSolutionAsync(workspace, path, cancellationToken);
        var symbols = await FindSymbolsAsync(solution, symbolName, cancellationToken);
        var results = new List<SymbolReferenceResult>();

        foreach (var symbol in symbols)
        {
            if (includeDefinition)
                AddLocations(results, symbol, symbol.Locations.Where(location => location.IsInSource), solution, true);

            foreach (var referencedSymbol in await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken))
                foreach (var location in referencedSymbol.Locations)
                    AddLocation(results, symbol, location.Location, solution, false);
        }

        return results.Distinct().OrderBy(result => result.File, StringComparer.Ordinal)
            .ThenBy(result => result.Line).ThenBy(result => result.Column).ToArray();
    }

    public static Task<IReadOnlyList<SymbolReferenceResult>> FindUsagesAtPositionAsync(
        string path, string file, int line, int column, bool includeDefinition, CancellationToken cancellationToken = default)
    {
        if (line < 1 || column < 1) throw new ArgumentException("Line and column must be greater than zero.");
        return FindUsagesCoreAsync(path, file, line, column, includeDefinition, cancellationToken);
    }

    private static async Task<IReadOnlyList<SymbolReferenceResult>> FindUsagesCoreAsync(
        string path, string file, int line, int column, bool includeDefinition, CancellationToken cancellationToken)
    {
        var workspace = MSBuildWorkspace.Create();
        try
        {
            var solution = await OpenSolutionAsync(workspace, path, cancellationToken);
            var fullFile = Path.GetFullPath(file);
            var document = solution.Projects.SelectMany(project => project.Documents)
                .FirstOrDefault(candidate => string.Equals(candidate.FilePath, fullFile, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"File '{file}' is not part of the workspace.", nameof(file));
            var text = await document.GetTextAsync(cancellationToken);
            if (line > text.Lines.Count || column > text.Lines[line - 1].Span.Length + 1)
                throw new ArgumentException("Line or column is outside the file.");
            var position = text.Lines[line - 1].Start + column - 1;
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position, cancellationToken)
                ?? throw new ArgumentException("No symbol exists at that position.");
            var results = new List<SymbolReferenceResult>();
            if (includeDefinition) AddLocations(results, symbol, symbol.Locations.Where(location => location.IsInSource), solution, true);
            foreach (var referencedSymbol in await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken))
                foreach (var location in referencedSymbol.Locations) AddLocation(results, symbol, location.Location, solution, false);
            var orderedResults = results.Distinct().OrderBy(result => result.File).ThenBy(result => result.Line).ThenBy(result => result.Column).ToArray();
            workspace.Dispose();
            return orderedResults;
        }
        catch
        {
            workspace.Dispose();
            throw;
        }
    }

    public static async Task<IReadOnlyList<DiagnosticResult>> ValidateFilePathAsync(
        string path, string file, bool runAnalyzers, CancellationToken cancellationToken = default)
    {
        using var workspace = MSBuildWorkspace.Create();
        var solution = await OpenSolutionAsync(workspace, path, cancellationToken);
        var fullFile = Path.GetFullPath(file);
        var document = solution.Projects.SelectMany(project => project.Documents)
            .FirstOrDefault(candidate => string.Equals(candidate.FilePath, fullFile, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"File '{file}' is not part of the workspace.", nameof(file));
        var compilation = (await document.Project.GetCompilationAsync(cancellationToken))!;
        var diagnostics = compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => IsDiagnosticForFile(diagnostic, fullFile))
            .Select(diagnostic => CreateDiagnostic(diagnostic, "compiler")).ToList();
        if (runAnalyzers)
        {
            var analyzers = document.Project.AnalyzerReferences.SelectMany(reference => reference.GetAnalyzers(LanguageNames.CSharp)).ToImmutableArray();
            diagnostics.AddRange((await compilation.WithAnalyzers(analyzers, options: null).GetAnalyzerDiagnosticsAsync(cancellationToken))
                .Where(diagnostic => IsDiagnosticForFile(diagnostic, fullFile))
                .Select(diagnostic => CreateDiagnostic(diagnostic, "analyzer")));
        }
        return diagnostics.OrderByDescending(result => result.Severity).ThenBy(result => result.Line).ThenBy(result => result.Id).ToArray();
    }

    public static async Task<IReadOnlyList<SymbolInfoResult>> GetSymbolInfoPathAsync(
        string path, string symbolName, CancellationToken cancellationToken = default)
    {
        using var workspace = MSBuildWorkspace.Create();
        var solution = await OpenSolutionAsync(workspace, path, cancellationToken);
        var symbols = await FindSymbolsAsync(solution, symbolName, cancellationToken);
        return symbols.SelectMany(symbol => symbol.Locations.Where(location => location.IsInSource)
                .Select(location => CreateInfo(symbol, location)))
            .OrderBy(result => result.QualifiedName, StringComparer.Ordinal)
            .ThenBy(result => result.File, StringComparer.Ordinal).ToArray();
    }

    public static async Task<DependencyAnalysisResult> AnalyzeDependenciesPathAsync(
        string path, CancellationToken cancellationToken = default)
    {
        using var workspace = MSBuildWorkspace.Create();
        var solution = await OpenSolutionAsync(workspace, path, cancellationToken);
        var dependencies = new List<ProjectDependencyResult>();
        var usages = new List<NamespaceUsageResult>();

        foreach (var project in solution.Projects)
        {
            dependencies.AddRange(project.ProjectReferences.Select(reference =>
                new ProjectDependencyResult(project.Name, solution.GetProject(reference.ProjectId)!.Name, "project")));
            dependencies.AddRange(project.MetadataReferences.Select(reference =>
                new ProjectDependencyResult(project.Name, Path.GetFileNameWithoutExtension(reference.Display!), "assembly")));

            var namespaceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var document in project.Documents)
            {
                var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
                foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
                {
                    var name = directive.Name!.ToString();
                    namespaceCounts[name] = namespaceCounts.GetValueOrDefault(name) + 1;
                }
            }
            usages.AddRange(namespaceCounts.Select(pair => new NamespaceUsageResult(project.Name, pair.Key, pair.Value)));
        }

        return new(
            dependencies.Distinct().OrderBy(item => item.Project).ThenBy(item => item.Type).ThenBy(item => item.Dependency).ToArray(),
            usages.OrderBy(item => item.Project).ThenByDescending(item => item.Count).ThenBy(item => item.Namespace).ToArray());
    }

    public static async Task<IReadOnlyList<ComplexityResult>> AnalyzeComplexityPathAsync(
        string path, int threshold, int limit, CancellationToken cancellationToken = default)
    {
        if (threshold < 1) throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be greater than zero.");
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        using var workspace = MSBuildWorkspace.Create();
        var solution = await OpenSolutionAsync(workspace, path, cancellationToken);
        var results = new List<ComplexityResult>();

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        foreach (var document in project.Documents)
        {
            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            foreach (var method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
            {
                var complexity = CalculateCyclomaticComplexity(method);
                if (complexity < threshold) continue;
                var symbol = model.GetDeclaredSymbol(method, cancellationToken);
                var span = method.GetLocation().GetLineSpan();
                results.Add(new(project.Name, symbol!.ToDisplayString(QualifiedNameFormat),
                    span.Path, span.StartLinePosition.Line + 1, complexity));
            }
        }

        return results.OrderByDescending(result => result.Complexity).ThenBy(result => result.QualifiedName, StringComparer.Ordinal)
            .Take(limit).ToArray();
    }

    public static int CalculateCyclomaticComplexity(BaseMethodDeclarationSyntax method) => 1 +
        method.DescendantNodes().Count(node => node is IfStatementSyntax or WhileStatementSyntax or DoStatementSyntax or
            ForStatementSyntax or ForEachStatementSyntax or CatchClauseSyntax or ConditionalExpressionSyntax or
            SwitchExpressionArmSyntax || node is CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax) +
        method.DescendantTokens().Count(token => token.IsKind(SyntaxKind.AmpersandAmpersandToken) ||
            token.IsKind(SyntaxKind.BarBarToken) || token.IsKind(SyntaxKind.QuestionQuestionToken));

    private static async Task<Solution> OpenSolutionAsync(MSBuildWorkspace workspace, string path, CancellationToken cancellationToken)
    {
        MSBuildRegistration.Ensure();
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".csproj" => (await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken)).Solution,
            ".sln" or ".slnx" => await workspace.OpenSolutionAsync(path, cancellationToken: cancellationToken),
            _ => throw new ArgumentException("Path must point to a .sln, .slnx, or .csproj file.", nameof(path))
        };
    }

    private static async Task<IReadOnlyList<ISymbol>> FindSymbolsAsync(Solution solution, string symbolName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(symbolName)) throw new ArgumentException("Symbol name cannot be empty.", nameof(symbolName));
        var declarationName = symbolName.Split('(', 2)[0].Split('.').Last();
        var declarations = new List<ISymbol>();
        foreach (var project in solution.Projects)
            declarations.AddRange(await SymbolFinder.FindDeclarationsAsync(project, declarationName, ignoreCase: true, cancellationToken: cancellationToken));
        var symbols = declarations.Where(symbol => string.Equals(symbol.Name, symbolName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbol.ToDisplayString(QualifiedNameFormat), symbolName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (symbols.Length == 0) throw new ArgumentException($"Symbol '{symbolName}' was not found.", nameof(symbolName));
        return symbols;
    }

    private static void AddLocations(List<SymbolReferenceResult> results, ISymbol symbol, IEnumerable<Location> locations, Solution solution, bool definition)
    { foreach (var location in locations) AddLocation(results, symbol, location, solution, definition); }

    private static void AddLocation(List<SymbolReferenceResult> results, ISymbol symbol, Location location, Solution solution, bool definition)
    {
        var span = location.GetLineSpan();
        var document = solution.GetDocument(location.SourceTree!)!;
        results.Add(new(symbol.ToDisplayString(QualifiedNameFormat), document.Project.Name, span.Path,
            span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1, definition));
    }

    private static SymbolInfoResult CreateInfo(ISymbol symbol, Location location)
    {
        var span = location.GetLineSpan();
        var method = symbol as IMethodSymbol;
        return new(symbol.Name, symbol.ToDisplayString(QualifiedNameFormat),
            symbol is INamedTypeSymbol ? "type" : symbol.Kind.ToString().ToLowerInvariant(),
            symbol.DeclaredAccessibility.ToString().ToLowerInvariant(), symbol.ContainingNamespace.ToDisplayString(),
            symbol.ContainingType?.ToDisplayString(QualifiedNameFormat), method?.ReturnType.ToDisplayString(QualifiedNameFormat),
            method?.Parameters.Select(parameter => parameter.ToDisplayString(QualifiedNameFormat)).ToArray() ?? [],
            symbol.GetAttributes().Select(attribute => attribute.AttributeClass!.ToDisplayString(QualifiedNameFormat)).ToArray(),
            string.IsNullOrWhiteSpace(symbol.GetDocumentationCommentXml()) ? null : symbol.GetDocumentationCommentXml(),
            span.Path, span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1);
    }

    private static DiagnosticResult CreateDiagnostic(Diagnostic diagnostic, string source)
    {
        var span = diagnostic.Location.GetLineSpan();
        return new(diagnostic.Id, diagnostic.Severity.ToString().ToLowerInvariant(), diagnostic.GetMessage(), span.Path,
            span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1, source);
    }

    private static bool IsDiagnosticForFile(Diagnostic diagnostic, string fullFile) =>
        diagnostic.Location.SourceTree?.FilePath.Equals(fullFile, StringComparison.OrdinalIgnoreCase) == true;
}

internal static class MSBuildRegistration
{
    private static readonly bool Registered = Register();
    public static void Ensure() => _ = Registered;
    private static bool Register()
    {
        MSBuildLocator.RegisterDefaults();
        return true;
    }
}
