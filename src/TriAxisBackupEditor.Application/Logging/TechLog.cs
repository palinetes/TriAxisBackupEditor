using System.Globalization;
using System.Text;

namespace TriAxisBackupEditor.Application.Logging;

public sealed record LogEntry(DateTime Timestamp, string Level, string Message)
{
    public override string ToString() =>
        $"{Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} [{Level}] {Message}";
}

/// <summary>Log técnico: dispositivo, timestamps, bytes enviados/recebidos, erros e hashes.</summary>
public interface ITechLog
{
    /// <summary>Liga/desliga a gravação em arquivo. As entradas continuam visíveis na aba Log da sessão.</summary>
    bool FileLoggingEnabled { get; set; }

    string LogFolder { get; }

    event EventHandler<LogEntry>? EntryWritten;

    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}

public sealed class FileTechLog : ITechLog
{
    private readonly object _sync = new();

    public FileTechLog(string logFolder, bool enabled)
    {
        LogFolder = logFolder;
        FileLoggingEnabled = enabled;
    }

    public bool FileLoggingEnabled { get; set; }

    public string LogFolder { get; }

    public event EventHandler<LogEntry>? EntryWritten;

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("AVISO", message);

    public void Error(string message, Exception? exception = null) =>
        Write("ERRO", exception is null ? message : $"{message} | {exception.GetType().Name}: {exception.Message}");

    private void Write(string level, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, message);

        if (FileLoggingEnabled)
        {
            try
            {
                lock (_sync)
                {
                    Directory.CreateDirectory(LogFolder);
                    var file = Path.Combine(LogFolder, $"triaxis-{entry.Timestamp:yyyyMMdd}.log");
                    File.AppendAllText(file, entry + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Falha de log nunca deve interromper backup/restauração.
            }
        }

        EntryWritten?.Invoke(this, entry);
    }
}

public sealed class NullTechLog : ITechLog
{
    public static readonly NullTechLog Instance = new();

    public bool FileLoggingEnabled { get; set; }

    public string LogFolder => string.Empty;

    public event EventHandler<LogEntry>? EntryWritten
    {
        add { }
        remove { }
    }

    public void Info(string message)
    {
    }

    public void Warn(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
