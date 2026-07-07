using System.Diagnostics;
using System.Text;
using Npgsql;

namespace ECSPros.Api.Services;

public enum MigrationStatus { Idle, Running, Completed, Failed }

public class MigrationState
{
    public MigrationStatus Status { get; set; } = MigrationStatus.Idle;
    public int Phase { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Output { get; set; } = "";
    public string? Error { get; set; }
}

public class MigrationService
{
    private readonly IConfiguration _config;
    private readonly ILogger<MigrationService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly StringBuilder _outputBuffer = new();
    private MigrationState _state = new();

    public MigrationService(IConfiguration config, ILogger<MigrationService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public MigrationState GetState() => new()
    {
        Status = _state.Status,
        Phase = _state.Phase,
        StartedAt = _state.StartedAt,
        FinishedAt = _state.FinishedAt,
        Output = _outputBuffer.ToString(),
        Error = _state.Error
    };

    public async Task<bool> StartAsync(int phase)
    {
        if (!await _lock.WaitAsync(0)) return false; // zaten çalışıyor

        if (_state.Status == MigrationStatus.Running)
        {
            _lock.Release();
            return false;
        }

        _outputBuffer.Clear();
        _state = new MigrationState
        {
            Status = MigrationStatus.Running,
            Phase = phase,
            StartedAt = DateTime.UtcNow
        };

        _ = Task.Run(async () =>
        {
            try
            {
                var toolPath = Path.GetFullPath(
                    Path.Combine(AppContext.BaseDirectory, "../../../../tools/MigrationTool/MigrationTool.csproj"));

                // Production'da publish edilmiş binary'yi kullan
                var publishedBin = Path.GetFullPath(
                    Path.Combine(AppContext.BaseDirectory, "../../../../tools/MigrationTool/bin/Debug/net8.0/MigrationTool.dll"));

                string fileName;
                string arguments;

                if (File.Exists(publishedBin))
                {
                    fileName = "dotnet";
                    arguments = $"\"{publishedBin}\" {phase}";
                }
                else
                {
                    // Kaynak koddan çalıştır
                    var projDir = Path.GetFullPath(
                        Path.Combine(AppContext.BaseDirectory, "../../../../tools/MigrationTool"));
                    fileName = "dotnet";
                    arguments = $"run --project \"{projDir}\" -- {phase}";
                }

                _outputBuffer.AppendLine($"[Başlatıldı] dotnet {arguments}");

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = new Process { StartInfo = psi };

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) _outputBuffer.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) _outputBuffer.AppendLine("[STDERR] " + e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                _state.Status = process.ExitCode == 0 ? MigrationStatus.Completed : MigrationStatus.Failed;
                if (process.ExitCode != 0)
                    _state.Error = $"Exit code: {process.ExitCode}";
            }
            catch (Exception ex)
            {
                _state.Status = MigrationStatus.Failed;
                _state.Error = ex.Message;
                _outputBuffer.AppendLine("[HATA] " + ex.Message);
                _logger.LogError(ex, "Migration tool çalıştırılamadı");
            }
            finally
            {
                _state.FinishedAt = DateTime.UtcNow;
                _lock.Release();
            }
        });

        return true;
    }

    public async Task<Dictionary<string, long>> GetTableStatsAsync()
    {
        var conn = _config.GetConnectionString("DefaultConnection")!;
        var result = new Dictionary<string, long>();

        var tables = new (string schema, string table)[]
        {
            ("definition", "image_sets"), ("definition", "attribute_types"), ("definition", "attribute_values"),
            ("definition", "product_groups"), ("catalog", "products"), ("catalog", "product_attributes"),
            ("catalog", "product_variants"), ("catalog", "product_variant_attributes"), ("catalog", "product_images")
        };

        await using var pgConn = new NpgsqlConnection(conn);
        await pgConn.OpenAsync();

        foreach (var (schema, table) in tables)
        {
            await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {schema}.{table}", pgConn);
            result[table] = (long)(await cmd.ExecuteScalarAsync())!;
        }

        return result;
    }
}
