# Auditor cash custody and manager returns

Auditor acceptance confirms physical custody. It closes the teller session but leaves its cash position outstanding until Manager receipt. Auditor sessions are not created. Cash held is the sum of accepted handoffs with no destination session. Existing accepted handoffs are included automatically.

Cash Management > Cash Transfer is available only to the Auditor role with audit permission. An auditor selects whole handoffs for one business date and the receiving manager from a Manager-only selector. Managers acknowledge or decline returns in Session Handoff Requests under Manager Cash Receipts. Reserved handoffs cannot be selected again. Manager eligibility is enforced on the server with the vault permission. The designated manager accepts or declines using the shared note field; decline requires a reason.

Submission reserves the selection without moving cash. Acceptance creates VaultReturnOut and VaultReturnIn movements for each original source and the open vault, assigns the original handoff destination, and records history. Decline releases the selection and leaves custody with the auditor. Date locking serializes submission, receipt, and closing. Versions protect decisions. Original accepted handoff recipients remain unchanged.

Vault finalization and business-day closing block on accepted handoffs without destinations. Corrections can follow the original handoff destination and its existing vault-return movement pair after receipt.

## API

- `GET /api/cash-handoffs/auditor-return-managers`: active vault-authorized recipients; Auditor-only.
- `GET /api/cash-handoffs/auditor-returns?businessDate=yyyy-MM-dd`: visible return history.
- `POST /api/cash-handoffs/auditor-returns`: `{ managerId, handoffIds: [1, 2], note }`.
- `POST /api/cash-handoffs/auditor-returns/{id}/accept`: `{ note, expectedVersion }`.
- `POST /api/cash-handoffs/auditor-returns/{id}/decline`: `{ note, expectedVersion }`.

Run the AuditorCashReturns EF migration before using these endpoints. No existing custody or cash movement is rewritten by the migration.

## Integration acceptance checks

1. Close two teller sessions to an auditor and accept both. Verify vault cash is unchanged and both appear as held cash, including existing accepted handoffs.
2. Select both and send to a manager. Verify they remain held, cannot be reserved twice, and do not increase vault cash.
3. Accept as the designated manager. Verify each source decreases by its handoff amount, vault increases by the combined amount, and exactly one movement pair exists per handoff. Repeat acceptance: balances must remain unchanged.
4. Decline a fresh return with a reason. Verify no movements, custody unchanged, and handoffs selectable for resubmission. Empty decline reason must fail.
5. Attempt submission with another auditor's handoff, mixed dates, duplicate IDs, already delivered cash, or a recipient without vault authority. Each must fail atomically.
6. Attempt a decision as another manager or with a stale version. Each must fail without cash effects.
7. Try final vault count and business-day closing with outstanding auditor custody. Both must block; after receipt the custody blocker must clear.
8. Verify closed business dates reject submissions and decisions, and unavailable vault receipt rolls back the whole batch.
9. Recount/correct a returned source using the existing correction workflow; verify it locates the linked vault movement pair.
