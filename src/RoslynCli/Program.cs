using System.Diagnostics.CodeAnalysis;

namespace RoslynCli;

[ExcludeFromCodeCoverage(Justification = "Process entry point delegates directly to the tested application boundary.")]
internal static class Program
{
    public static Task<int> Main(string[] args) => RoslynCliApp.InvokeAsync(args);
}
