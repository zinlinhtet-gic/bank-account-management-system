# Accounting and End-of-Day Workflows

The Accounting navigation contains General Ledger, Journal Entries, Reconciliation, Cash Reconciliation, and End of Day. Pages use the existing MVVM flow: views bind to page ViewModels, ViewModels call typed client services, and services use the shared `ApiClient`.

## Reconciliation

The account reconciliation page is tab based: Account Results shows operational and ledger balances, differences, and statuses for the selected business-date range; Exceptions and Investigation lists matching exceptions and provides assignment, status, notes, correction references, and the append-only audit timeline. Both tabs share the date range and run through the existing reconciliation ViewModel and typed client service. Investigators cannot resolve exceptions directly. Reconciliation does not edit account or ledger balances. Server exceptions are translated through the shared typed API exception path.

## Cash reconciliation

The cash page and its commands require `cash_operations`; Vault opening and adjustment review/approval controls require `end_of_day_approval`. End of Day hides its cash tab without cash-operation permission, its pre-close action without accounting/audit permission, and approval/close actions without end-of-day approval permission.

The cash session date filter defaults to the current server business date, selects an open session automatically when one exists, and can be changed to load another business date's sessions.

Cash reconciliation is tab based: Sessions lists and opens positions; Physical Count records a selected session's physical balance; Transfers moves cash between selected sessions; Adjustments supports correction-linked requests and approvals; Session History shows movements and count audit rows. The existing cash operations ViewModel and client service back all tabs. Only one cash-position session may be opened for a business date across teller and vault positions, and closing it does not allow a second session for that date. Withdrawals that would make expected cash negative are rejected with a cash-specific error. Positions and reconciliation exceptions are not branch-scoped. End of Day loads cash sessions for its active business date so prior-day positions remain available for physical close. New sessions and postings use the active posting date; they are blocked only when yesterday is still open and today has no open business-date session. Session selection is made from the session list; users do not type session IDs into count or transfer controls. Session history exposes movements, linked transaction references, count results, actors, and approved correction references. Teller and vault expected amounts are server-owned. Cash transactions automatically post their physical movement to the actor's open teller session for the active business date; transaction forms do not ask for a session ID. A mismatch creates an exception for investigation. Adjustments link to an exact posted Cash on Hand effect and require independent approval. NRC pickup branch remains a separate transaction delivery location. Successful business-date close displays confirmation and the newly opened date.

## End of Day

The parent `EndOfDayViewModel` composes child ViewModels for summary, pre-close, cash, ledger/account reconciliation, the Exception Center, and final review. Pre-close stages expose blockers; approval and close controls require the corresponding permission and server-side state. Approval is dual control. The server rechecks blockers and generates the GL close audit while holding the business-date lock, then makes the next business date available. EOD is manually initiated; scheduled financial work and scheduled account reconciliation use the server's existing scheduler.

## Monthly accounting close alerts

The second day of each month at 00:00 Asia/Rangoon runs prior-month account maintenance and interest accumulation before generating `MonthlySummary` rows. If the month's final business date is still open, the scheduler fails with a readable cause after automatic retry attempts are exhausted. Managers with `scheduled_job_management` see the alert in the application header, navigate to the normal End of Day flow to close the date, then retry the same scheduled occurrence. Each request and attempt is retained by the server; the alert clears after a successful retry. Monthly totals use raw accounting entries, while previous-period openings prefer `MonthlySummary`, then later `DailySummary`, and then raw accounting history where no snapshots exist.
