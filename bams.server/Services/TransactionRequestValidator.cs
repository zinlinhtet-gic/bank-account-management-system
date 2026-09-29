using bams.server.Constants;
using bams.server.Exceptions;
using bams.server.Messages;

namespace bams.server.Services;

/// <summary>
/// Request checks shared by the transaction services. Each check throws a <see cref="ValidationException"/>
/// with a stable message code; none of them touch the database.
/// </summary>
public static class TransactionRequestValidator
{
    // Amounts must be positive and fit the stored scale, so the posted amount equals the requested amount.
    public static void ValidateAmount(decimal amount)
    {
        if (amount <= 0m
            || decimal.Round(amount, TransactionConstants.MaximumAmountDecimalPlaces) != amount)
        {
            throw new ValidationException(MessageCode.InvalidAmount);
        }
    }

    // A transfer from an account to itself would only create two cancelling entries.
    public static void EnsureDifferentAccounts(long sourceAccountId, long destinationAccountId)
    {
        if (sourceAccountId == destinationAccountId)
        {
            throw new ValidationException(MessageCode.SameSourceAndDestinationAccount);
        }
    }

    // Rejects requests where any required text field is empty or whitespace.
    public static void EnsureRequired(params string?[] values)
    {
        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        }
    }

    // Rejects text that would not fit its database column, instead of failing on save with a 500.
    public static void EnsureMaximumLength(string? value, int maximumLength)
    {
        if (value is not null && value.Trim().Length > maximumLength)
        {
            throw new ValidationException(MessageCode.FieldTooLong);
        }
    }

    // Validates the optional description and reference number shared by every transaction request.
    public static void ValidateCommonFields(string? description, string? referenceNo)
    {
        EnsureMaximumLength(description, TransactionConstants.DescriptionMaximumLength);
        EnsureMaximumLength(referenceNo, TransactionConstants.ReferenceNoMaximumLength);
    }

    // Stores optional text trimmed, and stores blank text as null.
    public static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    // Resolves the requested page, rejecting page numbers below 1 and page sizes outside 1..MaximumPageSize.
    public static (int Page, int PageSize) ResolvePaging(int? page, int? pageSize)
    {
        var resolvedPage = page ?? TransactionConstants.FirstPageNumber;
        var resolvedPageSize = pageSize ?? TransactionConstants.DefaultPageSize;

        if (resolvedPage < TransactionConstants.FirstPageNumber
            || resolvedPageSize < 1
            || resolvedPageSize > TransactionConstants.MaximumPageSize)
        {
            throw new ValidationException(MessageCode.InvalidRequest);
        }

        return (resolvedPage, resolvedPageSize);
    }

    // A "from" instant must be before the exclusive "before" instant when both are given.
    public static void ValidateDateRange(DateTimeOffset? from, DateTimeOffset? before)
    {
        if (from is not null && before is not null && from >= before)
        {
            throw new ValidationException(MessageCode.InvalidDateRange);
        }
    }
}
