using bams.server.Data;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Models.Customers;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Resolves and records account referrers.</summary>
public sealed class AccountRefererService : IAccountRefererService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICustomerLookUpService _customerLookUpService;

    public AccountRefererService(ApplicationDbContext dbContext, ICustomerLookUpService customerLookUpService)
    {
        _dbContext = dbContext;
        _customerLookUpService = customerLookUpService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Customer>> ResolveAndValidateReferersAsync(
        IReadOnlyList<string>? refererNrcs,
        int requiredCount,
        CancellationToken cancellationToken)
    {
        if (requiredCount < 0)
        {
            throw new ValidationException(MessageCode.InvalidRequest);
        }

        var nrcs = refererNrcs ?? [];
        if (nrcs.Any(string.IsNullOrWhiteSpace))
        {
            throw new ValidationException(MessageCode.AccountRefererSelectionInvalid);
        }

        var normalizedNrcs = nrcs.Select(nrc => nrc.Trim()).ToArray();
        if (normalizedNrcs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedNrcs.Length)
        {
            throw new ValidationException(MessageCode.AccountRefererSelectionInvalid);
        }

        if (normalizedNrcs.Length < requiredCount)
        {
            throw new ValidationException(MessageCode.AccountRefererCountInsufficient);
        }

        var referers = new List<Customer>(normalizedNrcs.Length);
        foreach (var nrc in normalizedNrcs)
        {
            referers.Add(await _customerLookUpService.FindRegisteredCustomerByNrcAsync(nrc, cancellationToken));
        }

        var refererIds = referers.Select(referer => referer.Id).ToArray();
        var customersWithOwnedAccounts = await _dbContext.AccountHolders.AsNoTracking()
            .Where(holder => refererIds.Contains(holder.CustomerId))
            .Select(holder => holder.CustomerId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var qualifyingRefererIds = customersWithOwnedAccounts.ToHashSet();
        if (referers.Any(referer => !qualifyingRefererIds.Contains(referer.Id)))
        {
            throw new ValidationException(MessageCode.AccountRefererMustOwnAccount);
        }

        return referers;
    }

    /// <inheritdoc />
    public Task CreateAccountReferersAsync(
        Account account,
        IReadOnlyList<Customer> referers,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _dbContext.AccountReferers.AddRange(referers.Select(referer => new AccountReferer
        {
            Account = account,
            CustomerId = referer.Id
        }));
        return Task.CompletedTask;
    }
}
