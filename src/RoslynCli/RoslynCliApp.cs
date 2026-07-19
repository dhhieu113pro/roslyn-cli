using System.CommandLine;

namespace RoslynCli;

public delegate Task<IReadOnlyList<SymbolSearchResult>> SearchSymbols(string path, string pattern, string? kind, int limit, CancellationToken cancellationToken);
public delegate Task<IReadOnlyList<SymbolReferenceResult>> FindReferences(string path, string symbol, bool includeDefinition, CancellationToken cancellationToken);
public delegate Task<IReadOnlyList<SymbolInfoResult>> GetSymbolInfo(string path, string symbol, CancellationToken cancellationToken);
public delegate Task<DependencyAnalysisResult> AnalyzeDependencies(string path, CancellationToken cancellationToken);
public delegate Task<IReadOnlyList<ComplexityResult>> AnalyzeComplexity(string path, int threshold, int limit, CancellationToken cancellationToken);
public delegate Task<IReadOnlyList<SymbolReferenceResult>> FindUsages(string path, string file, int line, int column, bool includeDefinition, CancellationToken cancellationToken);
public delegate Task<IReadOnlyList<DiagnosticResult>> ValidateFile(string path, string file, bool runAnalyzers, CancellationToken cancellationToken);
public delegate Task<string> RunExtendedTool(string path, string operation, string parametersJson, CancellationToken cancellationToken);

public static class RoslynCliApp
{
    public static Task<int> InvokeAsync(string[] args, TextWriter? output = null, TextWriter? error = null,
        SearchSymbols? search = null, CancellationToken cancellationToken = default,
        FindReferences? references = null, GetSymbolInfo? info = null,
        AnalyzeDependencies? dependencies = null, AnalyzeComplexity? complexity = null,
        FindUsages? usages = null, ValidateFile? validate = null, RunExtendedTool? runTool = null) =>
        CreateRootCommand(output, error, search, references, info, dependencies, complexity, usages, validate, runTool)
            .Parse(args).InvokeAsync(cancellationToken: cancellationToken);

    public static RootCommand CreateRootCommand(TextWriter? output = null, TextWriter? error = null,
        SearchSymbols? search = null, FindReferences? references = null, GetSymbolInfo? info = null,
        AnalyzeDependencies? dependencies = null, AnalyzeComplexity? complexity = null,
        FindUsages? usages = null, ValidateFile? validate = null, RunExtendedTool? runTool = null)
    {
        output ??= Console.Out; error ??= Console.Error;
        search ??= SymbolSearchService.SearchPathAsync;
        references ??= RoslynAnalysisService.FindReferencesPathAsync;
        info ??= RoslynAnalysisService.GetSymbolInfoPathAsync;
        dependencies ??= RoslynAnalysisService.AnalyzeDependenciesPathAsync;
        complexity ??= RoslynAnalysisService.AnalyzeComplexityPathAsync;
        usages ??= RoslynAnalysisService.FindUsagesAtPositionAsync;
        validate ??= RoslynAnalysisService.ValidateFilePathAsync;
        runTool ??= ExtendedToolService.ExecuteAsync;

        var searchPath = PathArgument(); var pattern = new Argument<string>("pattern") { Description = "Case-insensitive substring or wildcard (* and ?) pattern." };
        var searchFormat = FormatOption(); var kind = new Option<string?>("--kind") { Description = "Optional symbol kind: type, method, property, field, or event." };
        var searchLimit = LimitOption();
        var searchCommand = new Command("search", "Search source declarations semantically.") { searchPath, pattern, searchFormat, kind, searchLimit };
        searchCommand.SetAction((parse, token) => Execute(error, async () =>
        {
            var format = ValidateFormat(parse.GetValue(searchFormat)!); var limit = ValidatePositive(parse.GetValue(searchLimit), "--limit");
            OutputWriter.WriteSearch(await search(parse.GetValue(searchPath)!.FullName, parse.GetValue(pattern)!, parse.GetValue(kind), limit, token), format, output);
        }));

        var referencePath = PathArgument(); var referenceSymbol = SymbolArgument(); var referenceFormat = FormatOption();
        var excludeDefinition = new Option<bool>("--exclude-definition") { Description = "Return usages without declaration locations." };
        var referenceCommand = new Command("references", "Find all references to an exact symbol name.") { referencePath, referenceSymbol, referenceFormat, excludeDefinition };
        referenceCommand.SetAction((parse, token) => Execute(error, async () =>
        {
            var format = ValidateFormat(parse.GetValue(referenceFormat)!);
            OutputWriter.WriteReferences(await references(parse.GetValue(referencePath)!.FullName, parse.GetValue(referenceSymbol)!, !parse.GetValue(excludeDefinition), token), format, output);
        }));

        var infoPath = PathArgument(); var infoSymbol = SymbolArgument(); var infoFormat = FormatOption();
        var infoCommand = new Command("info", "Get detailed information for an exact symbol name.") { infoPath, infoSymbol, infoFormat };
        infoCommand.SetAction((parse, token) => Execute(error, async () =>
        {
            var format = ValidateFormat(parse.GetValue(infoFormat)!);
            OutputWriter.WriteSymbolInfo(await info(parse.GetValue(infoPath)!.FullName, parse.GetValue(infoSymbol)!, token), format, output);
        }));

        var usagePath = PathArgument(); var usageFile = FileArgument(); var usageFormat = FormatOption();
        var line = new Option<int>("--line") { Description = "One-based source line.", Required = true };
        var column = new Option<int>("--column") { Description = "One-based source column.", Required = true };
        var usageExcludeDefinition = new Option<bool>("--exclude-definition") { Description = "Return usages without declaration locations." };
        var usageCommand = new Command("usages", "Find usages of the symbol at a source position.") { usagePath, usageFile, line, column, usageFormat, usageExcludeDefinition };
        usageCommand.SetAction((parse, token) => Execute(error, async () =>
        {
            var format = ValidateFormat(parse.GetValue(usageFormat)!); var actualLine = ValidatePositive(parse.GetValue(line), "--line"); var actualColumn = ValidatePositive(parse.GetValue(column), "--column");
            OutputWriter.WriteReferences(await usages(parse.GetValue(usagePath)!.FullName, parse.GetValue(usageFile)!.FullName, actualLine, actualColumn, !parse.GetValue(usageExcludeDefinition), token), format, output);
        }));

        var validatePath = PathArgument(); var validateFile = FileArgument(); var validateFormat = FormatOption();
        var noAnalyzers = new Option<bool>("--no-analyzers") { Description = "Run compiler diagnostics only." };
        var validateCommand = new Command("validate", "Validate a C# file in project context.") { validatePath, validateFile, validateFormat, noAnalyzers };
        validateCommand.SetAction((parse, token) => Execute(error, async () =>
        {
            var format = ValidateFormat(parse.GetValue(validateFormat)!);
            OutputWriter.WriteDiagnostics(await validate(parse.GetValue(validatePath)!.FullName, parse.GetValue(validateFile)!.FullName, !parse.GetValue(noAnalyzers), token), format, output);
        }));

        var dependencyPath = PathArgument(); var dependencyFormat = FormatOption();
        var dependencyCommand = new Command("dependencies", "Analyze project, assembly, and namespace dependencies.") { dependencyPath, dependencyFormat };
        dependencyCommand.SetAction((parse, token) => Execute(error, async () =>
        {
            var format = ValidateFormat(parse.GetValue(dependencyFormat)!);
            OutputWriter.WriteDependencies(await dependencies(parse.GetValue(dependencyPath)!.FullName, token), format, output);
        }));

        var complexityPath = PathArgument(); var complexityFormat = FormatOption(); var threshold = new Option<int>("--threshold") { Description = "Minimum cyclomatic complexity.", DefaultValueFactory = _ => 5 }; var complexityLimit = LimitOption();
        var complexityCommand = new Command("complexity", "Find methods at or above a cyclomatic-complexity threshold.") { complexityPath, complexityFormat, threshold, complexityLimit };
        complexityCommand.SetAction((parse, token) => Execute(error, async () =>
        {
            var format = ValidateFormat(parse.GetValue(complexityFormat)!); var actualThreshold = ValidatePositive(parse.GetValue(threshold), "--threshold"); var limit = ValidatePositive(parse.GetValue(complexityLimit), "--limit");
            OutputWriter.WriteComplexity(await complexity(parse.GetValue(complexityPath)!.FullName, actualThreshold, limit, token), format, output);
        }));

        var symbolCommand = new Command("symbol", "Inspect C# symbols.") { searchCommand, referenceCommand, infoCommand, usageCommand };
        var analyzeCommand = new Command("analyze", "Analyze C# solutions and projects.") { dependencyCommand, complexityCommand, validateCommand };
        var toolPath = PathArgument(); var operation = new Argument<string>("operation") { Description = "Extended Roslyn operation name." };
        var parameters = new Option<string>("--params") { Description = "Operation parameters as a JSON object.", DefaultValueFactory = _ => "{}" };
        var toolFormat = FormatOption();
        var toolRunCommand = new Command("run", "Run any extended Roslyn query or previewable refactoring.") { toolPath, operation, parameters, toolFormat };
        toolRunCommand.SetAction((parse, token) => Execute(error, async () =>
        {
            var format = ValidateFormat(parse.GetValue(toolFormat)!);
            OutputWriter.WriteRawJson(await runTool(parse.GetValue(toolPath)!.FullName, parse.GetValue(operation)!, parse.GetValue(parameters)!, token), format, output);
        }));
        var toolListCommand = new Command("list", "List all extended Roslyn operations.");
        toolListCommand.SetAction((_, _) => { foreach (var name in ExtendedToolService.GetOperationNames()) output.WriteLine(name); return Task.FromResult(0); });
        var toolCommand = new Command("tool", "Run the complete Roslyn refactoring and analysis operation set.") { toolRunCommand, toolListCommand };
        return new RootCommand("Semantic command-line tools for C# and Roslyn.") { symbolCommand, analyzeCommand, toolCommand };
    }

    private static async Task<int> Execute(TextWriter error, Func<Task> action)
    {
        try { await action(); return 0; }
        catch (ArgumentException exception) { error.WriteLine(exception.Message); return 2; }
        catch (Exception exception) { error.WriteLine(exception.Message); return 1; }
    }

    private static string ValidateFormat(string format)
    { if (!OutputWriter.IsSupportedFormat(format)) throw new ArgumentException("--format must be either 'text' or 'json'."); return format; }
    private static int ValidatePositive(int value, string option)
    { if (value < 1) throw new ArgumentException($"{option} must be greater than zero."); return value; }
    private static Argument<FileInfo> PathArgument() => new("path") { Description = "Path to a .sln, .slnx, or .csproj file." };
    private static Argument<string> SymbolArgument() => new("symbol") { Description = "Exact simple or fully-qualified symbol name." };
    private static Argument<FileInfo> FileArgument() => new("file") { Description = "Path to a C# source file in the workspace." };
    private static Option<string> FormatOption() => new("--format") { Description = "Output format: text or json.", DefaultValueFactory = _ => "text" };
    private static Option<int> LimitOption() => new("--limit") { Description = "Maximum number of results.", DefaultValueFactory = _ => 100 };
}
