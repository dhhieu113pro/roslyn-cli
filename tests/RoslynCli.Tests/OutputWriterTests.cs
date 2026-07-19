using System.Text.Json;

namespace RoslynCli.Tests;

public sealed class OutputWriterTests
{
    private static readonly SymbolSearchResult Result = new(
        "Run",
        "Demo.Worker.Run()",
        "method",
        "Demo",
        "/src/Worker.cs",
        12,
        7);

    [Theory]
    [InlineData("text", true)]
    [InlineData("TEXT", true)]
    [InlineData("json", true)]
    [InlineData("JSON", true)]
    [InlineData("xml", false)]
    public void IsSupportedFormat_RecognizesFormats(string format, bool expected) =>
        Assert.Equal(expected, OutputWriter.IsSupportedFormat(format));

    [Fact]
    public void WriteSearch_WritesTextLocation()
    {
        using var writer = new StringWriter();

        OutputWriter.WriteSearch([Result], "TEXT", writer);

        Assert.Equal(
            $"method   Demo.Worker.Run()  /src/Worker.cs:12:7{Environment.NewLine}",
            writer.ToString());
    }

    [Fact]
    public void WriteSearch_WritesVersionedJsonEnvelope()
    {
        using var writer = new StringWriter();

        OutputWriter.WriteSearch([Result], "JSON", writer);

        using var document = JsonDocument.Parse(writer.ToString());
        var root = document.RootElement;
        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(1, root.GetProperty("count").GetInt32());
        var item = Assert.Single(root.GetProperty("results").EnumerateArray());
        Assert.Equal("Run", item.GetProperty("name").GetString());
        Assert.Equal("Demo.Worker.Run()", item.GetProperty("qualifiedName").GetString());
        Assert.Equal("method", item.GetProperty("kind").GetString());
        Assert.Equal("Demo", item.GetProperty("project").GetString());
        Assert.Equal("/src/Worker.cs", item.GetProperty("file").GetString());
        Assert.Equal(12, item.GetProperty("line").GetInt32());
        Assert.Equal(7, item.GetProperty("column").GetInt32());
    }

    [Fact]
    public void WriteSearch_WritesEmptyText()
    {
        using var writer = new StringWriter();

        OutputWriter.WriteSearch([], "text", writer);

        Assert.Empty(writer.ToString());
    }
}
