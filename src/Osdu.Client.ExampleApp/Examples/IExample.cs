namespace Osdu.Client.ExampleApp.Examples;

/// <summary>
/// Represents a runnable OSDU client example.
/// </summary>
public interface IExample
{
    /// <summary>
    /// Display name shown on the sidebar button.
    /// </summary>
    string Text { get; }

    /// <summary>
    /// Short description shown below the title when selected.
    /// </summary>
    string ShortDescription { get; }

    /// <summary>
    /// The source code of the RunAsync method for display purposes.
    /// </summary>
    string SourceCode { get; }

    /// <summary>
    /// Executes the example and returns the result as a string.
    /// </summary>
    Task<string> RunAsync(CancellationToken cancellationToken = default);
}
