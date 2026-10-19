using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;

namespace bams.server.Services;

/// <summary>Pure schedule invariants shared by persistence workflows and regression tests.</summary>
public static class CashScheduleValidation
{
    /// <summary>Rejects invalid periods, amounts and overlap before any cash reservation is written.</summary>
    public static void Validate(IReadOnlyList<ScheduledCashSessionRequest> rows, DateOnly date)
    {
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.OfficerId <= 0 || row.StartAtMyanmar.Kind != DateTimeKind.Unspecified || row.EndAtMyanmar.Kind != DateTimeKind.Unspecified ||
                DateOnly.FromDateTime(row.StartAtMyanmar) != date || DateOnly.FromDateTime(row.EndAtMyanmar) != date || row.EndAtMyanmar <= row.StartAtMyanmar)
                throw new ValidationException(MessageCode.InvalidRequest);
            if (row.StartingCash <= 0m || decimal.Round(row.StartingCash, 2) != row.StartingCash)
                throw new ValidationException(MessageCode.InvalidAmount);
            if (rows.Take(index).Any(other => other.OfficerId == row.OfficerId && other.StartAtMyanmar < row.EndAtMyanmar && other.EndAtMyanmar > row.StartAtMyanmar))
                throw new ConflictException(MessageCode.CashSessionAlreadyExists);
        }
    }
}
