using System.Diagnostics;
using System.Text.RegularExpressions;
using Wolverine;

namespace Foundry.Shared.Infrastructure.Middleware;

public static partial class WolverineModuleTaggingMiddleware
{
    public static void Before(Envelope envelope)
    {
        if (Activity.Current is not { } activity)
        {
            return;
        }

        string? ns = envelope.Message?.GetType().Namespace;
        if (ns is null)
        {
            return;
        }

        Match match = ModuleNamePattern().Match(ns);
        if (match.Success)
        {
            activity.SetTag("foundry.module", match.Groups[1].Value);
        }
    }

    [GeneratedRegex(@"^Foundry\.(\w+)\.(Application|Infrastructure)\b", RegexOptions.NonBacktracking)]
    private static partial Regex ModuleNamePattern();
}
