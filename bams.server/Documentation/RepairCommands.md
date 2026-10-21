# Server repair commands

Run from `bams.server` using the server's normal environment and connection-string configuration:

```powershell
dotnet run --repair:account-opening-cash
```

This previews missing cash movements for completed positive account-opening deposits. To explicitly separate .NET CLI arguments from server arguments, the equivalent form is `dotnet run -- --repair:account-opening-cash`.

Apply eligible repairs with an active manager's user ID for audit attribution:

```powershell
dotnet run --repair:account-opening-cash --apply --actor-id=1
dotnet run --repair:account-opening-cash --apply --actor-id=1 --include-closed
```

Replace `1` with the actual manager ID. Optional `--transaction-id=123` limits either preview or apply to one original transaction. `--include-closed` permits changes to the original closed session, never the teller's current session. It can also be used during preview. Closed sessions are skipped without it.

The command runs once and exits without HTTP listeners, background jobs, migrations, or startup seeders. The schema must already be current. No command runs automatically during normal server startup.

## Repair rules and output

- Candidates must have the account-opening description, a completed positive CashDeposit transaction, one matching account credit, the account's opening timestamp, the original account number as reference, and a valid balanced opening journal. No new transaction or accounting/customer balance entry is posted.
- Match exactly one original teller session by initiating user, posting business date, activation time, and closure time. Missing or ambiguous sessions are reported for manual investigation; the command never creates a session or guesses a different teller/date.
- Existing movement links (including prior corrections), reconciliation exclusions, and pending missing-movement proposals are skipped. This command does not approve, resolve, or waive reconciliation exceptions.
- Each repair appends one approved Deposit movement linked to the original transaction, increases that session's ExpectedClosingCash and version, and writes an `account.opening_cash_repair` audit log attributed to the supplied manager. The original teller remains the movement actor; the manager is its approver. The movement timestamp records repair time and its note preserves the original posting timestamp.
- Applying uses a database transaction per deposit and row locks, rechecking movement links after locking. A rerun skips existing links. Previously committed repairs remain if a later deposit fails.
- Closed-session status, physical counts, handoffs, finalized summaries, account balances, and accounting entries remain unchanged. Recheck affected cash discrepancies through the normal reconciliation workflow after repair.

Each candidate produces a JSON result containing its transaction ID, status, and, where applicable, session ID, amount, and expected cash before/after. `Ready` means preview eligibility; `Repaired` means committed; `AlreadyLinked` means no change. Other statuses require review, including `NoMatchingSession`, `AmbiguousSession`, `ClosedSessionRequiresFlag`, `OpeningEvidenceMismatch`, `PostingDateMismatch`, `SourceChanged`, `ExcludedByReconciliation`, and `PendingCorrection`.

Exit codes: `0` completed with no unresolved candidates; `1` candidates need review or a requested transaction was not a candidate; `2` invalid arguments, invalid apply actor, or an execution failure. Application failures include their stable message code. Use `dotnet run -- --repair:account-opening-cash --help` for syntax without connecting to the database.
