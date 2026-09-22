using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Customers;
using bams.server.Models.Products;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class FixedDepositService : IFixedDepositService
{
    private const string ActiveRuleStatus = "Active";

    private readonly ApplicationDbContext _dbContext;
    private readonly IAccountHolderService _accountHolderService;
    private readonly IAuditLogService _auditLogService;

    public FixedDepositService(
        ApplicationDbContext dbContext,
        IAccountHolderService accountHolderService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _accountHolderService = accountHolderService;
        _auditLogService = auditLogService;
    }

    /// <inheritdoc />
    public async Task<FixedDeposit> CreateFixedDepositAsync(
        Account account,
        AccountType accountType,
        Customer primaryHolder,
        CreateAccountRequest request,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        ValidateFixedDepositCreationRequest(request);
        var startDate = DateOnly.FromDateTime(createdAt);
        var rateRule = await GetApplicableInterestRateRuleAsync(
            request.InterestRateRuleId.GetValueOrDefault(),
            accountType.Id,
            request.OpeningBalance,
            startDate,
            cancellationToken);
        var payoutAccount = await ResolvePayoutAccountAsync(
            request.PayoutAccountId,
            accountType,
            primaryHolder,
            cancellationToken);
        var fixedDeposit = BuildFixedDeposit(
            account,
            rateRule,
            payoutAccount,
            request,
            startDate,
            createdAt);
        await InsertFixedDepositWithAuditAsync(
            fixedDeposit,
            request.CreatedBy,
            createdAt,
            cancellationToken);
        return fixedDeposit;
    }

    /// <inheritdoc />
    public async Task<FixedDepositResponse> UpdateFixedDepositAsync(
        long fixedDepositId,
        UpdateFixedDepositRequest request,
        long changedBy,
        CancellationToken cancellationToken)
    {
        ValidateFixedDepositUpdateRequest(request);
        var fixedDeposit = await GetFixedDepositForUpdateAsync(fixedDepositId, cancellationToken);
        var oldResponse = ToResponse(fixedDeposit);
        var oldStatus = ParseStatus(fixedDeposit.Status);
        ValidateFixedDepositCanBeUpdated(oldStatus);
        var updatedAt = DateTime.UtcNow;
        ApplyFixedDepositFieldUpdates(fixedDeposit, request);
        await ApplyPayoutAccountUpdateAsync(fixedDeposit, request.PayoutAccountId, cancellationToken);
        var renewedDeposit = await ApplyStatusUpdateAsync(
            fixedDeposit, oldStatus, request.Status, updatedAt, cancellationToken);
        fixedDeposit.UpdatedAt = updatedAt;
        return await PersistFixedDepositUpdateWithAuditAsync(
            fixedDeposit, renewedDeposit, oldResponse, changedBy, updatedAt, cancellationToken);
    }

    // Ensures all fixed-deposit-only creation values are present.
    private static void ValidateFixedDepositCreationRequest(CreateAccountRequest request)
    {
        if (!request.InterestRateRuleId.HasValue ||
            !request.RenewalInstruction.HasValue ||
            !request.CalculateFromCurrent.HasValue)
        {
            throw new ValidationException(MessageCode.FixedDepositRequestInvalid);
        }
    }

    // Builds a fixed-deposit entity from validated account, rate, and payout data.
    private static FixedDeposit BuildFixedDeposit(
        Account account,
        InterestRateRule rateRule,
        Account payoutAccount,
        CreateAccountRequest request,
        DateOnly startDate,
        DateTime createdAt)
    {
        return new FixedDeposit
        {
            AccountId = account.Id,
            PrincipalAmount = request.OpeningBalance,
            InterestRateRuleId = rateRule.Id,
            AppliedAnnualRate = rateRule.AnnualRate,
            StartDate = startDate,
            MaturityDate = CalculateMaturityDate(startDate, rateRule.TermDays, rateRule.TermMonths),
            TermDays = rateRule.TermDays,
            TermMonths = rateRule.TermMonths,
            RenewalInstruction = request.RenewalInstruction!.Value,
            PayoutAccountId = payoutAccount.Id,
            Status = FixedDepositStatus.Active.ToString(),
            OriginalPrincipal = request.OpeningBalance,
            CurrentPrincipal = request.OpeningBalance,
            CalculateFromCurrent = request.CalculateFromCurrent!.Value,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    // Persists a fixed deposit and its creation audit entry within the caller's transaction.
    private async Task InsertFixedDepositWithAuditAsync(
        FixedDeposit fixedDeposit,
        long createdBy,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        await _dbContext.FixedDeposits.AddAsync(fixedDeposit, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.RecordFixedDepositCreationLogAsync(
            ToResponse(fixedDeposit),
            createdBy,
            createdAt,
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    

    // Rejects update requests that do not supply any mutable fixed-deposit field.
    private static void ValidateFixedDepositUpdateRequest(UpdateFixedDepositRequest request)
    {
        if (request.RenewalInstruction is null && request.PayoutAccountId is null &&
            request.CurrentPrincipal is null && request.CalculateFromCurrent is null && request.Status is null)
        {
            throw new ValidationException(MessageCode.FixedDepositUpdateRequiresChanges);
        }
    }

    // Loads the fixed deposit and account type required by update validation.
    private async Task<FixedDeposit> GetFixedDepositForUpdateAsync(
        long fixedDepositId,
        CancellationToken cancellationToken)
    {
        var fixedDeposit = await _dbContext.FixedDeposits
            .Include(deposit => deposit.Account)
                .ThenInclude(account => account!.AccountType)
            .FirstOrDefaultAsync(deposit => deposit.Id == fixedDepositId, cancellationToken);
        if (fixedDeposit is null)
        {
            throw new NotFoundException(MessageCode.FixedDepositNotFound);
        }
        return fixedDeposit;
    }

    // Prevents any further mutation after a fixed deposit reaches a terminal status.
    private static void ValidateFixedDepositCanBeUpdated(FixedDepositStatus status)
    {
        if (status is FixedDepositStatus.Closed or FixedDepositStatus.Cancelled)
        {
            throw new BusinessRuleException(MessageCode.FixedDepositStatusTransitionNotAllowed);
        }
    }

    // Applies scalar updates after validating the current principal.
    private static void ApplyFixedDepositFieldUpdates(
        FixedDeposit fixedDeposit,
        UpdateFixedDepositRequest request)
    {
        if (request.CurrentPrincipal.HasValue)
        {
            if (request.CurrentPrincipal.Value < 0)
            {
                throw new ValidationException(MessageCode.FixedDepositCurrentPrincipalInvalid);
            }
            fixedDeposit.CurrentPrincipal = request.CurrentPrincipal.Value;
        }
        if (request.RenewalInstruction.HasValue)
        {
            fixedDeposit.RenewalInstruction = request.RenewalInstruction.Value;
        }
        if (request.CalculateFromCurrent.HasValue)
        {
            fixedDeposit.CalculateFromCurrent = request.CalculateFromCurrent.Value;
        }
    }

    // Validates and applies a requested payout account change.
    private async Task ApplyPayoutAccountUpdateAsync(
        FixedDeposit fixedDeposit,
        long? payoutAccountId,
        CancellationToken cancellationToken)
    {
        if (!payoutAccountId.HasValue)
        {
            return;
        }
        var primaryCustomer = await GetPrimaryCustomerAsync(fixedDeposit.AccountId, cancellationToken);
        var payoutAccount = await ResolvePayoutAccountAsync(
            payoutAccountId,
            fixedDeposit.Account!.AccountType!,
            primaryCustomer,
            cancellationToken);
        fixedDeposit.PayoutAccountId = payoutAccount.Id;
    }

    // Applies a valid status transition and prepares a successor for renewable maturity.
    private async Task<FixedDeposit?> ApplyStatusUpdateAsync(
        FixedDeposit fixedDeposit,
        FixedDepositStatus oldStatus,
        FixedDepositStatus? requestedStatus,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        if (!requestedStatus.HasValue || requestedStatus.Value == oldStatus)
        {
            return null;
        }
        ValidateStatusTransition(oldStatus, requestedStatus.Value);
        fixedDeposit.Status = requestedStatus.Value.ToString();
        if (requestedStatus.Value != FixedDepositStatus.Matured ||
            fixedDeposit.RenewalInstruction == RenewalInstruction.NoRenewal)
        {
            return null;
        }
        var renewedDeposit = CreateRenewedDeposit(fixedDeposit, updatedAt);
        await _dbContext.FixedDeposits.AddAsync(renewedDeposit, cancellationToken);
        return renewedDeposit;
    }

    // Persists the update, renewal, and audit entries in one database transaction.
    private async Task<FixedDepositResponse> PersistFixedDepositUpdateWithAuditAsync(
        FixedDeposit fixedDeposit,
        FixedDeposit? renewedDeposit,
        FixedDepositResponse oldResponse,
        long changedBy,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        var newResponse = ToResponse(fixedDeposit);
        await _auditLogService.RecordFixedDepositUpdateLogAsync(
            oldResponse, newResponse, changedBy, updatedAt, cancellationToken);
        if (renewedDeposit is not null)
        {
            await _auditLogService.RecordFixedDepositCreationLogAsync(
                ToResponse(renewedDeposit), changedBy, updatedAt, cancellationToken);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return newResponse;
    }

    private async Task<InterestRateRule> GetApplicableInterestRateRuleAsync(long ruleId, long accountTypeId, decimal balance, DateOnly startDate, CancellationToken cancellationToken)
    {
        var rule = await _dbContext.InterestRateRules.AsNoTracking()
            .FirstOrDefaultAsync(existingRule => existingRule.Id == ruleId, cancellationToken);
        if (rule is null)
        {
            throw new NotFoundException(MessageCode.InterestRateRuleNotFound);
        }

        if (rule.AccountTypeId != accountTypeId || rule.Status != ActiveRuleStatus ||
            rule.EffectiveFrom > startDate || (rule.EffectiveTo.HasValue && rule.EffectiveTo.Value < startDate) ||
            (rule.BalanceMin.HasValue && balance < rule.BalanceMin.Value) ||
            (rule.BalanceMax.HasValue && balance > rule.BalanceMax.Value))
        {
            throw new BusinessRuleException(MessageCode.InterestRateRuleNotApplicable);
        }

        CalculateMaturityDate(startDate, rule.TermDays, rule.TermMonths);
        return rule;
    }

    private async Task<Account> ResolvePayoutAccountAsync(long? payoutAccountId, AccountType requestedType, Customer primaryHolder, CancellationToken cancellationToken)
    {
        if (!payoutAccountId.HasValue)
        {
            return await _accountHolderService.FindRequiredIndividualAccountAsync(requestedType, primaryHolder, cancellationToken);
        }

        if (!requestedType.RequiredProductId.HasValue)
        {
            throw new BusinessRuleException(MessageCode.RequiredPayoutAccountNotConfigured);
        }

        var payoutAccount = await _dbContext.AccountHolders.AsNoTracking()
            .Where(holder => holder.AccountId == payoutAccountId.Value &&
                holder.CustomerId == primaryHolder.Id && holder.OwnershipType == OwnershipType.Individual &&
                holder.Account != null && holder.Account.Status == AccountStatus.Active &&
                holder.Account.AccountTypeId == requestedType.RequiredProductId.Value)
            .Select(holder => holder.Account!)
            .FirstOrDefaultAsync(cancellationToken);
        if (payoutAccount is null)
        {
            throw new NotFoundException(MessageCode.PayoutAccountNotFound);
        }
        return payoutAccount;
    }

    private async Task<Customer> GetPrimaryCustomerAsync(long accountId, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.AccountHolders.AsNoTracking()
            .Where(holder => holder.AccountId == accountId && holder.IsPrimary)
            .Select(holder => holder.Customer)
            .FirstOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException(MessageCode.CustomerNotFound);
        }
        return customer;
    }

    private static DateOnly CalculateMaturityDate(DateOnly startDate, int? termDays, int? termMonths)
    {
        var hasDays = termDays is > 0;
        var hasMonths = termMonths is > 0;
        if (hasDays == hasMonths)
        {
            throw new BusinessRuleException(MessageCode.InterestRateRuleNotApplicable);
        }
        return hasDays ? startDate.AddDays(termDays!.Value) : startDate.AddMonths(termMonths!.Value);
    }

    private static void ValidateStatusTransition(FixedDepositStatus oldStatus, FixedDepositStatus newStatus)
    {
        var allowed = oldStatus switch
        {
            FixedDepositStatus.Active => newStatus is FixedDepositStatus.Matured or FixedDepositStatus.Closed or FixedDepositStatus.Cancelled,
            FixedDepositStatus.Matured => newStatus == FixedDepositStatus.Closed,
            _ => false
        };
        if (!allowed)
        {
            throw new BusinessRuleException(MessageCode.FixedDepositStatusTransitionNotAllowed);
        }
    }

    private static FixedDeposit CreateRenewedDeposit(FixedDeposit maturedDeposit, DateTime createdAt)
    {
        var principal = maturedDeposit.CalculateFromCurrent
            ? maturedDeposit.CurrentPrincipal
            : maturedDeposit.OriginalPrincipal;
        var startDate = DateOnly.FromDateTime(createdAt);
        return new FixedDeposit
        {
            AccountId = maturedDeposit.AccountId,
            PrincipalAmount = principal,
            InterestRateRuleId = maturedDeposit.InterestRateRuleId,
            AppliedAnnualRate = maturedDeposit.AppliedAnnualRate,
            StartDate = startDate,
            MaturityDate = CalculateMaturityDate(startDate, maturedDeposit.TermDays, maturedDeposit.TermMonths),
            TermDays = maturedDeposit.TermDays,
            TermMonths = maturedDeposit.TermMonths,
            RenewalInstruction = maturedDeposit.RenewalInstruction,
            PayoutAccountId = maturedDeposit.PayoutAccountId,
            Status = FixedDepositStatus.Active.ToString(),
            OriginalPrincipal = principal,
            CurrentPrincipal = principal,
            CalculateFromCurrent = maturedDeposit.CalculateFromCurrent,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    private static FixedDepositStatus ParseStatus(string status)
    {
        if (!Enum.TryParse<FixedDepositStatus>(status, out var parsedStatus))
        {
            throw new BusinessRuleException(MessageCode.FixedDepositStatusTransitionNotAllowed);
        }
        return parsedStatus;
    }

    private static FixedDepositResponse ToResponse(FixedDeposit deposit)
    {
        return new FixedDepositResponse(deposit.Id, deposit.AccountId, deposit.PrincipalAmount,
            deposit.InterestRateRuleId, deposit.AppliedAnnualRate, deposit.StartDate, deposit.MaturityDate,
            deposit.TermDays, deposit.TermMonths, deposit.RenewalInstruction, deposit.PayoutAccountId,
            ParseStatus(deposit.Status), deposit.OriginalPrincipal, deposit.CurrentPrincipal,
            deposit.CalculateFromCurrent, deposit.CreatedAt, deposit.UpdatedAt);
    }
}
