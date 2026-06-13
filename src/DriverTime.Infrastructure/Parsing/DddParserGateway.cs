using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DriverTime.Application.DDD;
using DriverTime.Application.DDD.DTOs;
using DriverTime.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace DriverTime.Infrastructure.Parsing;

public sealed class DddParserGateway : IDddParserGateway
{
    private readonly DddParserOptions _options;

    public DddParserGateway(IOptions<DddParserOptions> options)
    {
        _options = options.Value;
    }

    public async Task<DddParseResultDto> ParseAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("DDD file not found.", filePath);

        if (string.IsNullOrWhiteSpace(_options.ParserScriptPath))
            throw new InvalidOperationException("DDD parser path is not configured.");

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.PythonExecutable,
            Arguments = $"\"{_options.ParserScriptPath}\" \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = Process.Start(startInfo);

        if (process is null)
            throw new InvalidOperationException("Could not start DDD parser process.");

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"DDD parser failed: {stderr}");

        var result = JsonSerializer.Deserialize<DddParseResultDto>(
            stdout,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return result ?? throw new InvalidOperationException("DDD parser returned invalid JSON.");
    }
}