namespace DriverTime.Infrastructure.Parsing;

public sealed class DddParserOptions
{
    public string PythonExecutable { get; set; } = "python";

    public string ParserScriptPath { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;
}