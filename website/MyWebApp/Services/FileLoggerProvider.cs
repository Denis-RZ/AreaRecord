using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;

namespace MyWebApp.Services;

public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string filePath)
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(_filePath, name));
    }

    public void Dispose()
    {
    }
}

public class FileLogger : ILogger
{
    private readonly string _filePath;
    private readonly string _category;
    private static readonly object _lock = new();

    public FileLogger(string filePath, string category)
    {
        _filePath = filePath;
        _category = category;
    }

    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (formatter == null) return;
        var message = $"{DateTime.UtcNow:u} [{logLevel}] {_category}: {formatter(state, exception)}";
        if (exception != null) message += $"\n{exception}";
        lock (_lock)
        {
            File.AppendAllText(_filePath, message + Environment.NewLine);
        }
    }

    private class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
