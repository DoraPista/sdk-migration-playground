using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Gym.TestUtilities;

public sealed record CapturedLog(
    string Category,
    LogLevel Level,
    string Message,
    Exception? Exception,
    IReadOnlyList<KeyValuePair<string, object?>> State,
    IReadOnlyList<object?> Scopes)
{
    /// <summary>Everything a real log sink could end up writing: message, structured values, scopes, exception.</summary>
    public string FullText =>
        string.Join(" | ", new[] { Message, Exception?.ToString() ?? string.Empty }
            .Concat(State.Select(kv => $"{kv.Key}={kv.Value}"))
            .Concat(Scopes.Select(s => s?.ToString() ?? string.Empty)));
}

/// <summary>An <see cref="ILoggerProvider"/> that keeps every entry in memory.</summary>
public sealed class LogCapture : ILoggerProvider, ILoggerFactory
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();

    public IReadOnlyList<CapturedLog> Entries => _entries.ToArray();

    public string AllText => string.Join(Environment.NewLine, _entries.Select(e => e.FullText));

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    public ILogger<T> CreateLogger<T>() => new Logger<T>(this);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, LogCapture owner) : ILogger
    {
        private readonly AsyncLocal<Stack<object?>> _scopes = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            var stack = _scopes.Value ??= new Stack<object?>();
            stack.Push(state);
            return new PopScope(stack);
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state as IEnumerable<KeyValuePair<string, object?>> ?? Array.Empty<KeyValuePair<string, object?>>();
            var scopes = _scopes.Value?.ToArray() ?? Array.Empty<object?>();
            owner._entries.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception), exception, values.ToArray(), scopes));
        }

        private sealed class PopScope(Stack<object?> stack) : IDisposable
        {
            public void Dispose()
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
            }
        }
    }
}
