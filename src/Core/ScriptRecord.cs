using System;
using System.Collections.Generic;

namespace Prompter.Core;

/// <summary>
/// A single script in the local canonical library.
/// </summary>
/// <param name="Id">
/// Stable identifier for the script. Doubles as the Elgato Camera Hub GUID once the
/// script has been pushed, so local and remote identity never diverge.
/// </param>
/// <param name="Name">Human-friendly display name. Can change without affecting <see cref="Id"/>.</param>
/// <param name="Chapters">
/// Ordered chapter bodies. Matches the Camera Hub convention: each entry is one chapter;
/// single newlines inside an entry are soft line breaks, and chapters are separated from
/// each other by a blank line when serialized to text.
/// </param>
/// <param name="Order">Local display order (lower sorts first).</param>
/// <param name="CreatedUtc">Creation timestamp (UTC).</param>
/// <param name="UpdatedUtc">Last modification timestamp (UTC).</param>
public sealed record ScriptRecord(
    Guid Id,
    string Name,
    IReadOnlyList<string> Chapters,
    int Order,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc)
{
    /// <summary>Creates a brand-new record with a fresh id and current timestamps.</summary>
    public static ScriptRecord Create(string name, IReadOnlyList<string> chapters, int order)
    {
        var now = DateTimeOffset.UtcNow;
        return new ScriptRecord(Guid.NewGuid(), name, chapters, order, now, now);
    }

    public ScriptRecord WithChapters(IReadOnlyList<string> chapters)
        => this with { Chapters = chapters, UpdatedUtc = DateTimeOffset.UtcNow };

    public ScriptRecord WithName(string name)
        => this with { Name = name, UpdatedUtc = DateTimeOffset.UtcNow };

    public ScriptRecord WithOrder(int order)
        => this with { Order = order };
}
