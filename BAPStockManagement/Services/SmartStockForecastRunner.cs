using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace BAPStockManagement.Services;

public sealed record SmartStockForecastInput(
    DateOnly StartWeek, DateOnly ForecastWeek, IReadOnlyDictionary<string, double?[]> History);

public sealed record SmartStockForecast(string Model, double Quantity);

public sealed class SmartStockForecastRunner(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<SmartStockForecastRunner> logger)
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _forecastLock = new(1, 1);

    public async Task<IReadOnlyDictionary<string, SmartStockForecast>> PredictAsync(
        SmartStockForecastInput input, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Serialize(input, JsonOptions);
        var key = nameof(SmartStockForecastRunner) + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(request)));
        await _forecastLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue<Dictionary<string, SmartStockForecast>>(key, out var cached) && cached != null)
            {
                return cached;
            }

            var predictions = await RunAsync(request, cancellationToken);
            cache.Set(key, predictions, predictions.Count == input.History.Count
                ? TimeSpan.FromHours(1)
                : TimeSpan.FromMinutes(1));
            return predictions;
        }
        finally
        {
            _forecastLock.Release();
        }
    }

    private async Task<Dictionary<string, SmartStockForecast>> RunAsync(
        string request, CancellationToken cancellationToken)
    {
        var script = Path.Combine(environment.ContentRootPath, "Forecasting", "predict.py");
        var executable = configuration["SmartStockForecast:PythonExecutable"];
        var startInfo = new ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(executable) ? "python" : executable,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(script);
        using var process = new Process { StartInfo = startInfo };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.StandardInput.WriteAsync(request.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            var diagnostics = await errors;
            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(diagnostics))
            {
                logger.LogWarning("Smart Stock forecast worker exit code {ExitCode}. {Diagnostics}",
                    process.ExitCode, diagnostics);
            }

            var response = await output;
            return process.ExitCode == 0
                ? JsonSerializer.Deserialize<Dictionary<string, SmartStockForecast>>(response, JsonOptions) ?? []
                : [];
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Smart Stock forecast worker timed out");
            return [];
        }
        catch (Exception error) when (error is Win32Exception or IOException or JsonException)
        {
            logger.LogWarning(error, "Smart Stock forecast worker unavailable");
            return [];
        }
        finally
        {
            TryStop(process);
        }
    }

    private static void TryStop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process failed to start or already exited.
        }
        catch (Win32Exception)
        {
            // The process may have exited between the check and termination.
        }
    }
}
