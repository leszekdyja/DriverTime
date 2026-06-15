using System.Diagnostics;
using System.Text;
using System.Text.Json;
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
        {
            var parserError = GetParserError(stdout, stderr, process.ExitCode);
            throw new InvalidOperationException($"DDD parser failed: {parserError}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
            throw new InvalidOperationException("DDD parser returned empty output.");

        var json = ExtractJson(stdout);

        using var document = JsonDocument.Parse(json);

        var source = FindDataElement(document.RootElement);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var parsed = JsonSerializer.Deserialize<DddParseResultDto>(
            source.GetRawText(),
            options);

        return parsed ?? throw new InvalidOperationException("DDD parser returned invalid JSON.");
    }

    private static string ExtractJson(string text)
    {
        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');

        if (firstBrace < 0 || lastBrace < 0 || lastBrace <= firstBrace)
            throw new InvalidOperationException("DDD parser output does not contain JSON object.");

        return text.Substring(firstBrace, lastBrace - firstBrace + 1);
    }

    private static string GetParserError(string stdout, string stderr, int exitCode)
    {
        var details = new List<string>();

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            try
            {
                using var document = JsonDocument.Parse(ExtractJson(stdout));
                var root = document.RootElement;

                if (root.TryGetProperty("error", out var error)
                    && !string.IsNullOrWhiteSpace(error.GetString()))
                {
                    details.Add(error.GetString()!);
                }

                if (root.TryGetProperty("trace", out var trace)
                    && !string.IsNullOrWhiteSpace(trace.GetString()))
                {
                    details.Add(trace.GetString()!);
                }
            }
            catch (JsonException)
            {
                details.Add(stdout.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            details.Add(stderr.Trim());
        }

        return details.Count > 0
            ? string.Join(Environment.NewLine, details)
            : $"Process exited with code {exitCode}.";
    }

    private static JsonElement FindDataElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("activities", out _) ||
                element.TryGetProperty("vehicle_uses", out _) ||
                element.TryGetProperty("country_code_entries", out _))
            {
                return element;
            }

            foreach (var property in element.EnumerateObject())
            {
                var found = TryFindDataElement(property.Value);

                if (found.HasValue)
                    return found.Value;
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var found = TryFindDataElement(item);

                if (found.HasValue)
                    return found.Value;
            }
        }

        throw new InvalidOperationException("DDD parser JSON does not contain tachograph data.");
    }

    private static JsonElement? TryFindDataElement(JsonElement element)
    {
        try
        {
            return FindDataElement(element);
        }
        catch
        {
            return null;
        }
    }
}
