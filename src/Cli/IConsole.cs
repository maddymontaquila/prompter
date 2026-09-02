using System.IO;

namespace Prompter.Cli;

/// <summary>
/// Thin console I/O abstraction so CLI commands are testable without touching the real
/// <see cref="System.Console"/>.
/// </summary>
public interface IConsole
{
    TextWriter Out { get; }
    TextWriter Error { get; }
    TextReader In { get; }
    bool IsInputRedirected { get; }
}

/// <summary>Default <see cref="IConsole"/> backed by the real process console/streams.</summary>
public sealed class SystemConsole : IConsole
{
    public TextWriter Out => System.Console.Out;
    public TextWriter Error => System.Console.Error;
    public TextReader In => System.Console.In;
    public bool IsInputRedirected => System.Console.IsInputRedirected;
}

/// <summary>In-memory <see cref="IConsole"/> for tests.</summary>
public sealed class StringConsole : IConsole
{
    public StringWriter OutWriter { get; } = new();
    public StringWriter ErrorWriter { get; } = new();

    public TextWriter Out => OutWriter;
    public TextWriter Error => ErrorWriter;
    public TextReader In { get; init; } = TextReader.Null;
    public bool IsInputRedirected { get; init; }
}
