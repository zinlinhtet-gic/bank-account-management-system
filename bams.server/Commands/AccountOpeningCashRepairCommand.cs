using System.Globalization;

namespace bams.server.Commands;

/// <summary>Parses the standalone repair command; preview is the default and never writes data.</summary>
public sealed record AccountOpeningCashRepairCommand(bool Apply, bool IncludeClosed, long? ActorId, long? TransactionId)
{
    public const string Switch = "--repair:account-opening-cash";
    public const string Usage = "dotnet run --repair:account-opening-cash [--apply --actor-id=<manager-id>] [--include-closed] [--transaction-id=<id>]";

    /// <summary>Recognizes repair switches before ASP.NET's key/value argument parser runs.</summary>
    public static bool IsRequested(string[] args) => args.Any(arg => arg.StartsWith("--repair:", StringComparison.OrdinalIgnoreCase));

    /// <summary>Rejects unknown or duplicate arguments so mistyped write options cannot change the repair scope.</summary>
    public static AccountOpeningCashRepairCommand Parse(string[] args)
    {
        bool apply = false, includeClosed = false;
        long? actorId = null, transactionId = null;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var argument in args)
        {
            var parts = argument.Split('=', 2);
            if (!seen.Add(parts[0])) throw new ArgumentException("Duplicate repair argument: " + parts[0]);
            switch (parts[0].ToLowerInvariant())
            {
                case Switch when parts.Length == 1: break;
                case "--apply" when parts.Length == 1: apply = true; break;
                case "--include-closed" when parts.Length == 1: includeClosed = true; break;
                case "--actor-id" when parts.Length == 2: actorId = ParseId(parts[1]); break;
                case "--transaction-id" when parts.Length == 2: transactionId = ParseId(parts[1]); break;
                default: throw new ArgumentException("Unknown repair argument: " + argument);
            }
        }
        if (!seen.Contains(Switch)) throw new ArgumentException("Unknown repair command.");
        if (apply && actorId is null) throw new ArgumentException("--apply requires --actor-id=<manager-id> for audit attribution.");
        return new(apply, includeClosed, actorId, transactionId);
    }

    // Only positive invariant integer identifiers are accepted in command-line options.
    private static long ParseId(string value) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
        ? id : throw new ArgumentException("Repair identifiers must be positive integers.");
}
