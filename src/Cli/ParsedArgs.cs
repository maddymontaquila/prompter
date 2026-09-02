using System;
using System.Collections.Generic;

namespace Prompter.Cli;

/// <summary>
/// A deliberately small argument parser: options are <c>--name value</c> pairs or bare
/// <c>--flag</c> booleans, everything else is a positional. No external dependency is
/// pulled in for this - the surface prompter needs is tiny and stable, and a hand-rolled
/// parser keeps Native AOT publishing simple.
/// </summary>
public sealed class ParsedArgs
{
    private readonly Dictionary<string, string> _options;

    private ParsedArgs(Dictionary<string, string> options, IReadOnlyList<string> positionals)
    {
        _options = options;
        Positionals = positionals;
    }

    public IReadOnlyList<string> Positionals { get; }

    public static ParsedArgs Parse(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positionals = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (token.StartsWith("--", StringComparison.Ordinal) && token.Length > 2)
            {
                var name = token[2..];
                if (i + 1 < args.Count && !LooksLikeOption(args[i + 1]))
                {
                    options[name] = args[++i];
                }
                else
                {
                    options[name] = "true";
                }
            }
            else
            {
                positionals.Add(token);
            }
        }

        return new ParsedArgs(options, positionals);
    }

    private static bool LooksLikeOption(string token)
        => token.StartsWith("--", StringComparison.Ordinal) && token.Length > 2;

    /// <summary>Gets a string option value, or null if not supplied.</summary>
    public string? Get(string name) => _options.TryGetValue(name, out var value) ? value : null;

    /// <summary>Gets a string option value, or <paramref name="fallback"/> if not supplied.</summary>
    public string Get(string name, string fallback) => Get(name) ?? fallback;

    /// <summary>Returns true if the named boolean flag/option was supplied at all.</summary>
    public bool Has(string name) => _options.ContainsKey(name);

    /// <summary>True if the flag was supplied and is not explicitly set to "false".</summary>
    public bool Flag(string name)
        => _options.TryGetValue(name, out var value) &&
           !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
}
