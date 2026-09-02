namespace Prompter.Cli;

/// <summary>Process exit codes used consistently across every prompter CLI command.</summary>
public static class ExitCodes
{
    /// <summary>Command completed successfully.</summary>
    public const int Success = 0;

    /// <summary>Unexpected/internal failure.</summary>
    public const int GeneralError = 1;

    /// <summary>Bad arguments, missing required options, or conflicting options.</summary>
    public const int UsageError = 2;

    /// <summary>The requested script (or other resource) could not be found.</summary>
    public const int NotFound = 3;

    /// <summary>The request is ambiguous (e.g. multiple scripts share a name) or would conflict with existing data.</summary>
    public const int Conflict = 4;

    /// <summary>The command refused to proceed for safety (missing confirmation, Camera Hub running, schema drift, etc).</summary>
    public const int RefusedForSafety = 5;
}
