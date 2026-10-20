using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Security;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace bams.server.Services;

/// <summary>Records bank operating cash already spent; review and all posting effects commit together.</summary>
public sealed class BankExpenseService(ApplicationDbContext db, ICurrentUserService currentUser, LedgerPostingService ledger,
    IBusinessDateService businessDates, ReconciliationProjectionService projections,
    Notifications.INotificationService notifications)
{
    private const int MaximumReceiptBytes = 5 * 1024 * 1024;

    public async Task<BankExpenseResponse> SubmitAsync(SubmitBankExpenseRequest request, CancellationToken ct)
    {
        Validate(request);
        long actor = currentUser.GetCurrentUserId();
        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
        CashPositionSession session = await LockSessionAsync(request.SessionId, ct);
        BusinessDate? day = await db.BusinessDates.Where(x => x.Date == session.BusinessDate && x.IsActive && x.Status == OperationsConstants.BusinessDateOpen).SingleOrDefaultAsync(ct);
        if (day is null || session.Status != OperationsConstants.CashSessionOpen ||
            session.PositionType == OperationsConstants.CashPositionTeller && session.TellerId != actor)
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        if (session.PositionType == OperationsConstants.CashPositionVault && !await IsManagerAsync(actor, ct))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        BankExpenseRequest expense = CreateRequest(request, session.BusinessDate, actor);
        db.BankExpenseRequests.Add(expense);
        await db.SaveChangesAsync(ct);
        // The expense id exists only after the first save; both saves share the transaction.
        await NotifyExpenseSubmittedAsync(expense, actor, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToResponse(expense);
    }

    public async Task<IReadOnlyList<BankExpenseResponse>> ListAsync(DateOnly? date, CancellationToken ct)
    {
        long actor = currentUser.GetCurrentUserId();
        bool canReview = await IsManagerAsync(actor, ct);
        return await db.BankExpenseRequests.AsNoTracking().Where(x =>
                (canReview || x.RequestedBy == actor) && (!date.HasValue || x.BusinessDate == date.Value))
            .OrderByDescending(x => x.Id).Take(500).Select(x => new BankExpenseResponse(x.Id, x.SessionId,
                x.BusinessDate, x.Amount, x.Payee, x.Purpose, x.Status, x.ReceiptFileName,
                x.MissingReceiptReason, x.RequestedBy, x.RequestedAtUtc, x.ReviewedBy, x.ReviewedAtUtc,
                x.ReviewNote, x.TransactionId, x.ExceptionId)
            {
                PostingDate = x.TransactionId.HasValue ? x.Transaction!.BusinessDate :
                    db.BusinessDates.Any(day => day.Date == x.BusinessDate && day.Status == OperationsConstants.BusinessDateOpen)
                        ? x.BusinessDate : db.BusinessDates.Where(day => day.IsActive && day.Status == OperationsConstants.BusinessDateOpen &&
                            day.Date <= bams.server.Utils.BusinessTime.Today).Select(day => (DateOnly?)day.Date).SingleOrDefault()
            }).ToListAsync(ct);
    }

    public async Task<(byte[] Data, string Name, string ContentType)> ReceiptAsync(long id, CancellationToken ct)
    {
        BankExpenseRequest expense = await db.BankExpenseRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        long actor = currentUser.GetCurrentUserId();
        if (expense.RequestedBy != actor && !await IsManagerAsync(actor, ct))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        if (expense.ReceiptData is null) throw new NotFoundException(MessageCode.ResourceNotFound);
        return (expense.ReceiptData, expense.ReceiptFileName!, expense.ReceiptContentType!);
    }

    public async Task<BankExpenseResponse> ReviewAsync(long id, ReviewBankExpenseRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 2000)
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        long actor = currentUser.GetCurrentUserId();
        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
        // Match correction/EOD lock ordering and keep the selected active date stable until posting commits.
        await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Status = 'Open' ORDER BY Date FOR UPDATE").ToListAsync(ct);
        BankExpenseRequest expense = (await db.BankExpenseRequests.FromSql($"SELECT * FROM BankExpenseRequests WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (expense.Status != "PendingApproval") throw new ConflictException(MessageCode.Conflict);
        await RequireManagerApprovalAsync(actor, expense.RequestedBy, ct);
        expense.ReviewedBy = actor;
        expense.ReviewedAtUtc = DateTime.UtcNow;
        expense.ReviewNote = request.Note.Trim();
        if (request.Approve)
        {
            DateOnly postingDate = await businessDates.GetCorrectionPostingDateAsync(expense.BusinessDate, ct);
            CashPositionSession session = await LockSessionAsync(expense.SessionId, ct);
            // The expense was already spent before submission; approval may follow closure of its source session.
            if (session.Status != OperationsConstants.CashSessionOpen && session.Status != OperationsConstants.CashSessionClosed)
                throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
            await PostApprovedAsync(expense, session, actor, ct, postingDate);
            expense.Status = "Posted";
            await projections.GenerateProvisionalAsync(postingDate, ct);
        }
        else expense.Status = "Rejected";
        await NotifyExpenseReviewedAsync(expense, actor, request.Approve, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToResponse(expense);
    }

    // Asks every manager to review the expense; the copies disappear once one manager decides.
    private async Task NotifyExpenseSubmittedAsync(BankExpenseRequest expense, long actor, CancellationToken ct)
    {
        string requester = await notifications.GetUserNameAsync(actor, ct);
        await notifications.NotifyRoleAsync(SecurityConstants.ManagerRole, actor, new Notifications.NotificationMessage(
            NotificationConstants.Types.BankExpenseSubmitted, NotificationConstants.Categories.CashManagement,
            "Bank expense needs review",
            $"{requester} recorded a {expense.Amount:N2} expense paid to {expense.Payee} (expense #{expense.Id}).",
            NotificationConstants.Targets.BankExpense, expense.Id,
            NotificationConstants.Groups.BankExpenseReview(expense.Id)), ct);
    }

    // Tells the requester the manager's decision.
    private async Task NotifyExpenseReviewedAsync(BankExpenseRequest expense, long actor, bool approved, CancellationToken ct)
    {
        await notifications.DismissGroupAsync(NotificationConstants.Groups.BankExpenseReview(expense.Id), ct);
        string manager = await notifications.GetUserNameAsync(actor, ct);
        await notifications.NotifyUsersAsync([expense.RequestedBy], actor, new Notifications.NotificationMessage(
            approved ? NotificationConstants.Types.BankExpenseApproved : NotificationConstants.Types.BankExpenseRejected,
            NotificationConstants.Categories.CashManagement,
            approved ? "Bank expense approved" : "Bank expense rejected",
            $"{manager} {(approved ? "approved and posted" : "rejected")} expense #{expense.Id} ({expense.Amount:N2} to {expense.Payee}). Note: {expense.ReviewNote}",
            NotificationConstants.Targets.BankExpense, expense.Id), ct);
    }

    /// <summary>Shared posting operation used by ordinary requests and approved reconciliation proposals.</summary>
    public async Task PostApprovedAsync(BankExpenseRequest expense, CashPositionSession session, long approver, CancellationToken ct,
        DateOnly? postingDate = null)
    {
        if (expense.TransactionId.HasValue || session.BusinessDate != expense.BusinessDate ||
            session.ExpectedClosingCash - expense.Amount < session.ReservedCash)
            throw new BusinessRuleException(MessageCode.InsufficientCashPositionBalance);
        DateTime now = DateTime.UtcNow;
        Transaction transaction = LedgerPostingService.CreateTransaction(TransactionType.BankCashExpense,
            TransactionStatus.Completed, expense.Amount, expense.Purpose, $"BANK-EXP-{expense.Id}",
            $"BANK-EXP-{expense.Id}", expense.RequestedBy, now);
        DateOnly financialDate = postingDate ?? expense.BusinessDate;
        transaction.BusinessDate = financialDate;
        transaction.AuthorizedBy = approver;
        transaction.AuthorizedAt = now;
        long expenseGl = await db.GlAccounts.Where(x => x.Code == AccountingConstants.BankOperatingExpenseGlCode && x.Status == AccountingConstants.ActiveStatus)
            .Select(x => x.Id).SingleAsync(ct);
        long cashGl = await db.GlAccounts.Where(x => x.Code == AccountingConstants.CashOnHandGlCode && x.Status == AccountingConstants.ActiveStatus)
            .Select(x => x.Id).SingleAsync(ct);
        db.Transactions.Add(transaction);
        db.TransactionEntries.AddRange(
            new TransactionEntry { Transaction = transaction, GlAccountId = expenseGl, EntryType = EntryType.Debit,
                Amount = expense.Amount, PostingDate = financialDate, Description = expense.Purpose, CreatedAt = now },
            new TransactionEntry { Transaction = transaction, GlAccountId = cashGl, EntryType = EntryType.Credit,
                Amount = expense.Amount, PostingDate = financialDate, Description = expense.Purpose, CreatedAt = now });
        db.CashMovements.Add(new CashMovement { Session = session, Transaction = transaction, Type = "BankExpense",
            CashCorrectionProposalId = expense.CashProposalId,
            Amount = expense.Amount, ActorId = approver, ApprovedBy = approver, ApprovedAtUtc = now,
            CreatedAtUtc = now, Note = expense.Purpose });
        session.ExpectedClosingCash -= expense.Amount;
        session.Version++;
        expense.Transaction = transaction;
        await db.SaveChangesAsync(ct);
        await ledger.ValidateTransactionAccountingEntriesAsync(transaction.Id, ct);
    }

    public static BankExpenseRequest CreateRequest(SubmitBankExpenseRequest input, DateOnly date, long actor,
        long? exceptionId = null, long? cashProposalId = null)
    {
        Validate(input);
        return new BankExpenseRequest { SessionId = input.SessionId, BusinessDate = date, Amount = input.Amount,
            Payee = input.Payee.Trim(), Purpose = input.Purpose.Trim(), ReceiptFileName = input.ReceiptFileName,
            ReceiptContentType = input.ReceiptContentType, ReceiptData = string.IsNullOrWhiteSpace(input.ReceiptBase64)
                ? null : Convert.FromBase64String(input.ReceiptBase64), MissingReceiptReason = input.MissingReceiptReason?.Trim(),
            RequestedBy = actor, RequestedAtUtc = DateTime.UtcNow, ExceptionId = exceptionId, CashProposalId = cashProposalId };
    }

    public static void Validate(SubmitBankExpenseRequest input)
    {
        if (input.SessionId <= 0 || input.Amount <= 0m || decimal.Round(input.Amount, 2) != input.Amount ||
            string.IsNullOrWhiteSpace(input.Payee) || input.Payee.Length > 200 ||
            string.IsNullOrWhiteSpace(input.Purpose) || input.Purpose.Length > 500)
            throw new ValidationException(MessageCode.InvalidRequest);
        if (string.IsNullOrWhiteSpace(input.ReceiptBase64))
        {
            if (string.IsNullOrWhiteSpace(input.MissingReceiptReason) || input.MissingReceiptReason.Length > 500)
                throw new ValidationException(MessageCode.CorrectionEvidenceRequired);
        }
        else
        {
            if (input.ReceiptBase64.Length > MaximumReceiptBytes * 4 / 3 + 8 ||
                input.ReceiptContentType is not ("application/pdf" or "image/jpeg" or "image/png") ||
                string.IsNullOrWhiteSpace(input.ReceiptFileName) || input.ReceiptFileName.Length > 255)
                throw new ValidationException(MessageCode.InvalidRequest);
            byte[] bytes;
            try { bytes = Convert.FromBase64String(input.ReceiptBase64); }
            catch (FormatException) { throw new ValidationException(MessageCode.InvalidRequest); }
            if (bytes.Length == 0 || bytes.Length > MaximumReceiptBytes || !ValidSignature(bytes, input.ReceiptContentType))
                throw new ValidationException(MessageCode.InvalidRequest);
        }
    }

    private static bool ValidSignature(byte[] bytes, string contentType) => contentType switch
    {
        "application/pdf" => bytes.AsSpan().StartsWith("%PDF-"u8),
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        _ => false
    };

    private async Task RequireManagerApprovalAsync(long reviewer, long requester, CancellationToken ct)
    {
        List<long> managers = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
            join staff in db.Users on ur.UserId equals staff.Id
            where role.Code == SecurityConstants.ManagerRole && staff.Status == UserStatus.Active
            select staff.Id).Distinct().ToListAsync(ct);
        if (!managers.Contains(reviewer) || reviewer == requester && (managers.Count != 1 || managers[0] != reviewer))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
    }

    private Task<bool> IsManagerAsync(long actor, CancellationToken ct) =>
        (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
         where ur.UserId == actor && role.Code == SecurityConstants.ManagerRole select ur.UserId).AnyAsync(ct);

    private async Task<CashPositionSession> LockSessionAsync(long id, CancellationToken ct) =>
        (await db.CashPositionSessions.FromSql($"SELECT * FROM CashPositionSessions WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
        ?? throw new NotFoundException(MessageCode.ResourceNotFound);

    private static BankExpenseResponse ToResponse(BankExpenseRequest x) => new(x.Id, x.SessionId, x.BusinessDate,
        x.Amount, x.Payee, x.Purpose, x.Status, x.ReceiptFileName, x.MissingReceiptReason, x.RequestedBy,
        x.RequestedAtUtc, x.ReviewedBy, x.ReviewedAtUtc, x.ReviewNote, x.TransactionId, x.ExceptionId)
    {
        PostingDate = x.Transaction?.BusinessDate
    };
}
