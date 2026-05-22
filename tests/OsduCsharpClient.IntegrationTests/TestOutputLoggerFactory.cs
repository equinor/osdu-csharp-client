using Microsoft.Extensions.Logging;
using Xunit;

namespace OsduCsharpClient.IntegrationTests;

/// <summary>
/// A minimal <see cref="ILoggerFactory"/> that forwards SDK log records to the
/// xUnit <see cref="ITestOutputHelper"/> of whichever test is currently running.
/// The active helper is swapped per test via <see cref="SetOutput"/>, so HTTP
/// request/response logs appear under the test that produced them.
/// </summary>
public sealed class TestOutputLoggerFactory : ILoggerFactory
{
    private volatile ITestOutputHelper? _output;

    /// <summary>Routes subsequent log records to the given test's output.</summary>
    public void SetOutput(ITestOutputHelper output) => _output = output;

    public ILogger CreateLogger(string categoryName) =>
        new TestOutputLogger(categoryName, this);

    public void AddProvider(ILoggerProvider provider)
    {
        // No external providers — this factory is itself the sink.
    }

    public void Dispose()
    {
    }

    private sealed class TestOutputLogger(string category, TestOutputLoggerFactory owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var output = owner._output;
            if (output is null || !IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            try
            {
                output.WriteLine($"[{logLevel}] {category}: {message}");
                if (exception is not null)
                {
                    output.WriteLine(exception.ToString());
                }
            }
            catch (InvalidOperationException)
            {
                // ITestOutputHelper throws once its test has finished; ignore
                // log records emitted from late background continuations.
            }
        }
    }
}
