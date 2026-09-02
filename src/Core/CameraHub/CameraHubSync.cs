using System;
using System.Collections.Generic;
using System.Linq;

namespace Prompter.Core.CameraHub;

/// <summary>How to handle a pulled Camera Hub script whose id already exists locally.</summary>
public enum PullConflictPolicy
{
    /// <summary>Leave the local script untouched (default - never silently overwrite authored work).</summary>
    Skip,

    /// <summary>Replace the local script's name and chapters with Camera Hub's version.</summary>
    Overwrite,
}

/// <summary>Outcome of pulling a single Camera Hub entry.</summary>
public sealed record PullOutcome(Guid Id, string Action, string? Detail);

/// <summary>Aggregate result of a pull operation.</summary>
public sealed record PullSummary(IReadOnlyList<PullOutcome> Outcomes, bool CameraHubFound, string? FatalError)
{
    public bool Success => FatalError is null;
}

/// <summary>
/// Orchestrates Camera Hub push/pull against the local library. This is explicitly a
/// synchronization boundary, not the same thing as local export: the local library remains
/// the canonical store of authored scripts, and pulling never silently clobbers local
/// content - a conflict policy must be chosen explicitly to overwrite.
/// </summary>
public static class CameraHubSync
{
    /// <summary>
    /// Pulls Camera Hub scripts into the local library. Malformed Camera Hub entries are
    /// reported and skipped rather than aborting the whole pull. When <paramref name="onlyId"/>
    /// is set, only that id is considered.
    /// </summary>
    public static PullSummary Pull(
        CameraHubStore hubStore,
        LocalLibrary library,
        PullConflictPolicy conflictPolicy,
        Guid? onlyId = null)
    {
        var read = hubStore.ReadAll();
        if (!read.CameraHubDirectoryFound)
        {
            return new PullSummary([], false, null);
        }

        if (!read.Success)
        {
            return new PullSummary([], true, read.FatalError);
        }

        var outcomes = new List<PullOutcome>();
        var entries = onlyId is { } id ? read.Entries.Where(e => e.Id == id).ToList() : read.Entries;

        foreach (var entry in entries)
        {
            if (entry.Text is null)
            {
                outcomes.Add(new PullOutcome(entry.Id, "skipped-malformed", entry.Error));
                continue;
            }

            var existingLocal = library.Get(entry.Id);
            if (existingLocal is null)
            {
                library.Import(entry.Id, entry.Text.FriendlyName, entry.Text.Chapters, entry.PositionInLibraryList);
                outcomes.Add(new PullOutcome(entry.Id, "imported", null));
            }
            else if (conflictPolicy == PullConflictPolicy.Overwrite)
            {
                var updated = existingLocal.WithName(entry.Text.FriendlyName).WithChapters(entry.Text.Chapters);
                library.Save(updated);
                outcomes.Add(new PullOutcome(entry.Id, "overwritten", null));
            }
            else
            {
                outcomes.Add(new PullOutcome(
                    entry.Id,
                    "skipped-conflict",
                    "A local script with this id already exists. Re-run with --conflict overwrite to replace it."));
            }
        }

        return new PullSummary(outcomes, true, null);
    }

    /// <summary>Pushes one local script to Camera Hub (create-or-update).</summary>
    public static CameraHubWriteResult Push(CameraHubStore hubStore, ScriptRecord script)
        => hubStore.PushOne(script.Id, script.Name, script.Chapters);
}
