using System.Diagnostics;
using System.Text.Json;
using DriverTime.Application.DTOs.Ddd;
using DriverTime.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DriverTime.Infrastructure.Parsing;

public sealed class PythonDddParserGateway : IDddParserGateway
{
    private readonly IConfiguration _configuration;

    public PythonDddParserGateway(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<DddParseResultDto> ParseAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var pythonPath = _configuration["DddParser:PythonPath"] ?? "python";
        var scriptPath = _configuration["DddParser:ScriptPath"];

        if (string.IsNullOrWhiteSpace(scriptPath))
            throw new InvalidOperationException(
                "Missing DddParser:ScriptPath configuration.");

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"\"{scriptPath}\" \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start Python parser process.");

        var output = await process.StandardOutput
            .ReadToEndAsync(cancellationToken);

        var error = await process.StandardError
            .ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"DDD parser failed: {error}");
        }

        var result = JsonSerializer.Deserialize<DddParseResultDto>(
            output,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return result
            ?? throw new InvalidOperationException(
                "DDD parser returned empty or invalid JSON.");
    }
}
