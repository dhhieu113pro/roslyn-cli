using System.CommandLine;

namespace RoslynCli;

public delegate Task<IReadOnlyList<SymbolSearchResult>> SearchSymbols(
    string path,
    string pattern,
    string? kind,
    int limit,
    CancellationToken cancellationToken);

public static class RoslynCliApp
{
    public static Task<int> InvokeAsync(
        string[] args,
        TextWriter? output = null,
        TextWriter? error = null,
        SearchSymbols? search = null,
        CancellationToken cancellationToken = default) =>
        CreateRootCommand(output, error, search).Parse(args).InvokeAsync(cancellationToken: cancellationToken);

    public static RootCommand CreateRootCommand(
        TextWriter? output = null,
        TextWriter? error = null,
        SearchSymbols? search = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;
        search ??= SymbolSearchService.SearchPathAsync;

        var pathArgument = new Argument<FileInfo>("path")
        {
            Description = "Path to a .sln, .slnx, or .csproj file."
        };
        var patternArgument = new Argument<string>("pattern")
        {
            Description = "Case-insensitive substring to match against symbol names."
        };
        var formatOption = new Option<string>("--format")
        {
            Description = "Output format: text or json.",
            DefaultValueFactory = _ => "text"
        };
        var kindOption = new Option<string?>("--kind")
        {
            Description = "Optional symbol kind: type, method, property, field, or event."
        };
        var limitOption = new Option<int>("--limit")
        {
            Description = "Maximum number of results.",
            DefaultValueFactory = _ => 100
        };

        var searchCommand = new Command("search", "Search source declarations semantically.")
        {
            pathArgument,
            patternArgument,
            formatOption,
            kindOption,
            limitOption
        };

        searchCommand.SetAction(async (parseResult, actionCancellationToken) =>
        {
            var path = parseResult.GetValue(pathArgument)!;
            var pattern = parseResult.GetValue(patternArgument)!;
            var format = parseResult.GetValue(formatOption)!;
            var kind = parseResult.GetValue(kindOption);
            var limit = parseResult.GetValue(limitOption);

            if (limit < 1)
            {
                error.WriteLine("--limit must be greater than zero.");
                return 2;
            }

            if (!OutputWriter.IsSupportedFormat(format))
            {
                error.WriteLine("--format must be either 'text' or 'json'.");
                return 2;
            }

            try
            {
                var results = await search(
                    path.FullName,
                    pattern,
                    kind,
                    limit,
                    actionCancellationToken);
                OutputWriter.WriteSearch(results, format, output);
                return 0;
            }
            catch (ArgumentException exception)
            {
                error.WriteLine(exception.Message);
                return 2;
            }
            catch (Exception exception)
            {
                error.WriteLine(exception.Message);
                return 1;
            }
        });

        var symbolCommand = new Command("symbol", "Inspect C# symbols.") { searchCommand };
        return new RootCommand("Semantic command-line tools for C# and Roslyn.")
        {
            symbolCommand
        };
    }
}
