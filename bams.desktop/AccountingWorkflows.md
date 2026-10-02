# Accounting and End-of-Day Workflows

The Accounting navigation contains General Ledger, Journal Entries, Reconciliation, Cash Reconciliation, and End of Day. Pages use the existing MVVM flow: views bind to page ViewModels, ViewModels call typed client services, and services use the shared `ApiClient`.

## Reconciliation

The account reconciliation page selects a business-date range and optionally an account, displays a daily operational balance, ledger balance, difference, and status for every date in that range, and lists investigation exceptions. The Exception Center shows assignment and transaction references and exposes the append-only status/notes timeline. Investigators can update assignment, status, notes, and correction references; they cannot resolve exceptions directly. It is a discrepancy investigation screen; it does not edit account or ledger balances. Server exceptions are translated through the shared typed API exception path.

## Cash reconciliation

Cash operations list/open positions, transfer expected cash between positions, submit physical counts, and request/approve adjustments. Session history exposes movements, linked transaction references, count results, actors, and approved correction references. Teller and vault expected amounts are server-owned. Deposits, withdrawals, and NRC cash handling must provide an open cash-session ID so transaction posting and cash movement are recorded together. A mismatch creates an exception for investigation. Adjustments link to an exact posted Cash on Hand effect and require independent approval.

## End of Day

The parent `EndOfDayViewModel` composes child ViewModels for summary, pre-close, cash, ledger/account reconciliation, the Exception Center, and final review. Pre-close stages expose blockers; approval and close controls require the corresponding permission and server-side state. Approval is dual control. The server rechecks blockers and generates the GL close audit while holding the business-date lock, then makes the next business date available. EOD is manually initiated; scheduled financial work and scheduled account reconciliation use the server's existing scheduler.
