using Microsoft.Extensions.Logging;

namespace NabXliffApp.Services;

public sealed class AppLogService
{
    private readonly List<LogEntry> _entries = [];
    private readonly object _lock = new();
    private const int MaxEntries = 500;

    public event Action? OnNewEntry;

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock) { return _entries.ToList(); }
        }
    }

    public void Add(LogLevel level, string category, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, category, message);
        lock (_lock)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
                _entries.RemoveAt(0);
        }
        OnNewEntry?.Invoke();
    }

    public void Clear()
    {
        lock (_lock) { _entries.Clear(); }
        OnNewEntry?.Invoke();
    }
}

public record LogEntry(DateTime Timestamp, LogLevel Level, string Category, string Message);

/// <summary>
/// ILogger provider that writes to AppLogService so the Logs page can display them.
/// </summary>
public sealed class AppLogProvider : ILoggerProvider
{
    private readonly AppLogService _logService;

    public AppLogProvider(AppLogService logService)
    {
        _logService = logService;
    }

    public ILogger CreateLogger(string categoryName) => new AppLogger(_logService, categoryName);
    public void Dispose() { }

    private sealed class AppLogger : ILogger
    {
        private readonly AppLogService _logService;
        private readonly string _category;

        public AppLogger(AppLogService logService, string category)
        {
            _logService = logService;
            _category = SimplifyCategory(category);
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            if (exception is not null)
                message += $"\n{exception.GetType().Name}: {exception.Message}";

            _logService.Add(logLevel, _category, message);
        }

        private static string SimplifyCategory(string fullName)
        {
            // "NabXliffApp.Services.McpBridgeService" → "McpBridge"
            var name = fullName.Contains('.') ? fullName[(fullName.LastIndexOf('.') + 1)..] : fullName;
            return name.Replace("Service", "").Replace("Component", "");
        }
    }
}
