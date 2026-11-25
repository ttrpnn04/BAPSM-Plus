using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace BAPStockManagement.Services;

public sealed record SmartStockForecastInput(
    DateOnly StartWeek, DateOnly ForecastWeek, IReadOnlyDictionary<string, double?[]> History);

public sealed record SmartStockForecast(string Model, double Quantity);

public sealed record SmartStockForecastResult(
    IReadOnlyDictionary<string, SmartStockForecast> Predictions, string? FailureMessage = null);

public sealed class SmartStockForecastRunner(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<SmartStockForecastRunner> logger)
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _forecastLock = new(1, 1);

    public async Task<SmartStockForecastResult> PredictAsync(
        SmartStockForecastInput input, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Serialize(input, JsonOptions);
        var key = nameof(SmartStockForecastRunner) + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(request)));
        await _forecastLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue<SmartStockForecastResult>(key, out var cached) && cached != null)
            {
                return cached;
            }

            var result = await RunAsync(request, cancellationToken);
            cache.Set(key, result, result.Predictions.Count == input.History.Count
                ? TimeSpan.FromHours(1)
                : TimeSpan.FromMinutes(1));
            return result;
        }
        finally
        {
            _forecastLock.Release();
        }
    }

    private async Task<SmartStockForecastResult> RunAsync(
        string request, CancellationToken cancellationToken)
    {
        var script = Path.Combine(environment.ContentRootPath, "Forecasting", "predict.py");
        if (!File.Exists(script))
        {
            logger.LogWarning("Smart Stock forecast worker script is missing at {ScriptPath}", script);
            return Unavailable("เว็บยังไม่มีไฟล์รันโมเดลพยากรณ์");
        }
        var startInfo = new ProcessStartInfo
        {
            FileName = GetPythonExecutable(),
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
            IOException? inputError = null;
            try
            {
                await process.StandardInput.WriteAsync(request.AsMemory(), timeout.Token);
                process.StandardInput.Close();
            }
            catch (IOException error)
            {
                inputError = error;
            }
            await process.WaitForExitAsync(timeout.Token);
            var diagnostics = await errors;
            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(diagnostics))
            {
                logger.LogWarning("Smart Stock forecast worker exit code {ExitCode}. {Diagnostics}",
                    process.ExitCode, diagnostics);
            }

            var response = await output;
            if (process.ExitCode != 0)
            {
                return Unavailable("Python รันโมเดลไม่สำเร็จ กรุณาตรวจสอบเวอร์ชันและแพ็กเกจบนเครื่องที่รันเว็บ");
            }
            if (inputError != null)
            {
                throw inputError;
            }

            return new(JsonSerializer.Deserialize<Dictionary<string, SmartStockForecast>>(response, JsonOptions) ?? []);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Smart Stock forecast worker timed out");
            return Unavailable("โมเดลใช้เวลาพยากรณ์เกินกำหนด กรุณาลองอีกครั้ง");
        }
        catch (Win32Exception error)
        {
            logger.LogWarning(error, "Could not start Smart Stock Python executable {Executable}", startInfo.FileName);
            return Unavailable("เว็บเปิด Python สำหรับพยากรณ์ไม่ได้ กรุณาตรวจสอบการตั้งค่า Python ของเว็บ");
        }
        catch (Exception error) when (error is IOException or JsonException)
        {
            logger.LogWarning(error, "Could not read Smart Stock forecast worker output");
            return Unavailable("เว็บอ่านผลจากโมเดลพยากรณ์ไม่ได้");
        }
        finally
        {
            TryStop(process);
        }
    }

    private string GetPythonExecutable()
    {
        var configured = configuration["SmartStockForecast:PythonExecutable"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var windows = OperatingSystem.IsWindows();
        var local = windows
            ? Path.Combine(environment.ContentRootPath, "obj", "forecast-env", "Scripts", "python.exe")
            : Path.Combine(environment.ContentRootPath, "obj", "forecast-env", "bin", "python");
        if (File.Exists(local))
        {
            return local;
        }
        return windows ? "python" : "python3";
    }

    private static SmartStockForecastResult Unavailable(string message) => new(
        new Dictionary<string, SmartStockForecast>(), message);

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
