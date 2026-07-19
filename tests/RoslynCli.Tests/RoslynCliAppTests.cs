namespace RoslynCli.Tests;

public sealed class RoslynCliAppTests
{
    [Fact]
    public async Task InvokeAsync_UsesDefaultsAndWritesText()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        string? actualPath = null;
        string? actualPattern = null;
        string? actualKind = "unset";
        var actualLimit = 0;

        var exitCode = await RoslynCliApp.InvokeAsync(
            ["symbol", "search", "demo.csproj", "Worker"],
            output,
            error,
            (path, pattern, kind, limit, _) =>
            {
                actualPath = path;
                actualPattern = pattern;
                actualKind = kind;
                actualLimit = limit;
                return Task.FromResult<IReadOnlyList<SymbolSearchResult>>([
                    new("Worker", "Demo.Worker", "type", "Demo", "/src/Worker.cs", 1, 1)
                ]);
            });

        Assert.Equal(0, exitCode);
        Assert.Equal(Path.GetFullPath("demo.csproj"), actualPath);
        Assert.Equal("Worker", actualPattern);
        Assert.Null(actualKind);
        Assert.Equal(100, actualLimit);
        Assert.Contains("Demo.Worker", output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task InvokeAsync_PassesOptionsAndWritesJson()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        string? actualKind = null;
        var actualLimit = 0;

        var exitCode = await RoslynCliApp.InvokeAsync(
            ["symbol", "search", "demo.sln", "Run", "--kind", "method", "--limit", "4", "--format", "json"],
            output,
            error,
            (_, _, kind, limit, _) =>
            {
                actualKind = kind;
                actualLimit = limit;
                return Task.FromResult<IReadOnlyList<SymbolSearchResult>>([]);
            });

        Assert.Equal(0, exitCode);
        Assert.Equal("method", actualKind);
        Assert.Equal(4, actualLimit);
        Assert.Contains("\"schemaVersion\": \"1.0\"", output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task InvokeAsync_RejectsNonPositiveLimitWithoutSearching()
    {
        using var error = new StringWriter();
        var searched = false;

        var exitCode = await RoslynCliApp.InvokeAsync(
            ["symbol", "search", "demo.sln", "Run", "--limit", "0"],
            error: error,
            search: (_, _, _, _, _) =>
            {
                searched = true;
                return Task.FromResult<IReadOnlyList<SymbolSearchResult>>([]);
            });

        Assert.Equal(2, exitCode);
        Assert.False(searched);
        Assert.Contains("greater than zero", error.ToString());
    }

    [Fact]
    public async Task InvokeAsync_RejectsUnsupportedFormatWithoutSearching()
    {
        using var error = new StringWriter();
        var searched = false;

        var exitCode = await RoslynCliApp.InvokeAsync(
            ["symbol", "search", "demo.sln", "Run", "--format", "xml"],
            error: error,
            search: (_, _, _, _, _) =>
            {
                searched = true;
                return Task.FromResult<IReadOnlyList<SymbolSearchResult>>([]);
            });

        Assert.Equal(2, exitCode);
        Assert.False(searched);
        Assert.Contains("text", error.ToString());
    }

    [Fact]
    public async Task InvokeAsync_MapsArgumentExceptionToUsageError()
    {
        using var error = new StringWriter();

        var exitCode = await RoslynCliApp.InvokeAsync(
            ["symbol", "search", "demo.sln", "Run"],
            error: error,
            search: (_, _, _, _, _) => throw new ArgumentException("bad kind"));

        Assert.Equal(2, exitCode);
        Assert.Contains("bad kind", error.ToString());
    }

    [Fact]
    public async Task InvokeAsync_MapsUnexpectedExceptionToFailure()
    {
        using var error = new StringWriter();

        var exitCode = await RoslynCliApp.InvokeAsync(
            ["symbol", "search", "demo.sln", "Run"],
            error: error,
            search: (_, _, _, _, _) => throw new InvalidOperationException("load failed"));

        Assert.Equal(1, exitCode);
        Assert.Contains("load failed", error.ToString());
    }

    [Fact]
    public void CreateRootCommand_UsesConsoleDefaults()
    {
        var command = RoslynCliApp.CreateRootCommand();

        Assert.Equal("Semantic command-line tools for C# and Roslyn.", command.Description);
        Assert.Contains(command.Subcommands, child => child.Name == "symbol");
    }
}
