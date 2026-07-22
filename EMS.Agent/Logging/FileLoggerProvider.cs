using System.Collections.Concurrent;

namespace EMS.Agent.Logging;

/// <summary>
/// Minimal rolling-file logger. Used only by the usage-tracker process mode:
/// writing to the Windows Event Log from a non-elevated, per-user Scheduled
/// Task process has proven unreliable in practice (writes can silently fail
/// depending on the machine's Event Log ACLs), so this gives that process a
/// log destination that does not depend on those permissions.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly object _writeLock = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string filePath)
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _filePath, _writeLock));

    public void Dispose() => _loggers.Clear();

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly string _filePath;
        private readonly object _writeLock;

        public FileLogger(string category, string filePath, object writeLock)
        {
            _category = category;
            _filePath = filePath;
            _writeLock = writeLock;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_category}: {formatter(state, exception)}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            lock (_writeLock)
            {
                try
                {
                    File.AppendAllText(_filePath, line + Environment.NewLine);
                }
                catch
                {
                    // Best-effort diagnostic logging must never crash the app.
                }
            }
        }
    }
}
