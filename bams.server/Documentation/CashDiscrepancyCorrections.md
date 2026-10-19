# Cash discrepancy correction operations

## Investigation and approval

Run the unified reconciliation check, assign an exception, record findings and evidence, preview the selected discrepancy, and submit the proposal. An authorized manager approves the exact source snapshot. A proposer may approve only when that user is the sole active Manager. Posting commits before verification starts. A failed verification remains available for investigation and verification retry; retry never posts again.

`CashCorrectionService` coordinates approval and transactions. `CashCorrectionService.Planning.cs` contains a planner for each discrepancy. `CashCorrectionService.Operations.cs` contains the corresponding execution and verification functions. Shared helpers validate journals, preserve reserved cash, append movements, and compare physical counts.

## Required evidence and effects

All cases require `Findings` and `EvidenceReference`. An entered amount must have at most two decimal places. Financial cases can use the absolute detected difference as their suggested amount; recounts and corrected movement totals require an explicit amount.

| Discrepancy | Additional input | Approved operation |
| --- | --- | --- |
| CountEntryError | Amount, including zero | Append an approved physical count. No transaction or GL entry. |
| UnrecordedDeposit | AccountId, Amount | New deposit transaction, customer account transaction, balanced journal and incoming cash movement. |
| UnrecordedWithdrawal | AccountId, Amount | New withdrawal transaction, customer account transaction, balanced journal and outgoing cash movement. |
| MissingMovement | TransactionId | Validate the completed transaction and its journal; append only its missing net cash movement. |
| DuplicateMovement | MovementId, CounterpartMovementId | Identify the duplicate and the retained movement explicitly; append the duplicate's opposite cash effect. |
| IncorrectMovementAmount | MovementId, Amount | Amount is the corrected total, not a delta. Append the difference from the original movement. |
| WrongCashPosition | MovementId, DestinationSessionId | Append opposite effects to remove the financial movement from the wrong position and record it against the correct position. |
| TransferMismatch | MovementId, CounterpartMovementId, DestinationSessionId, Amount | Validate reciprocal TransferOut/TransferIn or VaultIssue/CashReceipt movements; independently calculate and append the required delta on both ends. |
| HandoffMismatch | MovementId, CounterpartMovementId, HandoffId, DestinationSessionId, Amount | Validate the accepted handoff, source count, reciprocal VaultReturnOut/VaultReturnIn records and original recorded amounts; append corrections on both ends. |
| UnexplainedOverShort | Amount | New balanced Cash on Hand/Cash Over Short transaction and matching movement. Amount must equal the absolute detected difference. |
| BankExpenseNotRecorded | Amount, Payee, Purpose, receipt or MissingReceiptReason | Reuse BankExpenseService to create a new expense request, expense transaction, balanced Bank Operating Expense/Cash on Hand journal and movement. |

Customer accounts must be active; debits preserve account ledger and available balances. Every affected cash position must retain at least its reserved cash. Paired positions must be different sessions on the same original business date. Unrelated source identifiers are rejected.

A duplicate must match its explicitly selected retained movement's session, type, amount, transaction and destination. Financial duplicates are checked against their existing journal. Custody duplicates require a supported custody movement type; the investigator's evidence and manager's decision establish which record is redundant.

Incorrect-amount and wrong-position cases apply to financial movements backed by an already valid journal. Custody pair errors use TransferMismatch or HandoffMismatch so both sides are considered. Changes to the financial journal itself belong to the account/GL correction workflow. Movement corrections do not repeat GL entries. Previously corrected sources and correction-generated movements cannot be reused as new correction sources; unresolved residual differences require a fresh investigation and the appropriate discrepancy.

## Source links and verification

New correction movements store `CashCorrectionProposalId`. Movements that correct an existing record also store `CorrectsMovementId`; missing financial movements link to the existing `TransactionId`. Original movements, transactions, journals and accepted handoffs are preserved. Nullable links leave historical rows intact.

Approval and submission lock business dates, the exception/proposal, affected sessions in ID order, and the selected source records. The source hash contains database evidence, including the counterpart, accepted handoff and destination count. The preview hash separately binds that evidence to the investigator's inputs and calculated posting date. Decimal request amounts are normalized before preview hashing. Proposals created with the earlier snapshot format must be refreshed and resubmitted; they must not bypass the additional source checks.

Paired verification compares the latest counts of both affected positions against their resulting expected cash. Missing counts or remaining differences fail verification. Financial verification also validates the posted journal. Missing-movement exceptions can clear when their required movement and journal are present; an independent cash-balance exception remains responsible for any physical mismatch.

Physical count calculations include transfers, vault issues/receipts and accepted vault returns after the observation. A linked custody correction takes effect at the original movement's occurrence time, regardless of when the correction was recorded. A correction to a custody movement before the latest count does not simulate another physical transfer after that count.

Financial corrections for a force-closed date use the current active open business date and retain the source exception/session. Recounts and movement-only repairs retain their original source date and need no new financial posting date. Finalized summaries are not rewritten.

## Deployment checkpoint

Apply the `CashMovementCorrectionSources` EF migration before using the updated service. It adds nullable proposal/source links and restrictive foreign keys to CashMovements. Migration generation does not apply the migration to a database. Force-close execution and audit changes are a separate review checkpoint.

## Evidence selection and review

Investigators select sources through the exception-scoped catalog and can inspect the original journal before previewing. Selection searches preserve already chosen source/counterpart records. Financial previews include proposed journal lines and customer effects; every affected cash position includes before/after expected and counted values. Managers receive the current source-hash comparison and original journal in a dedicated review form. A stale cash source disables acceptance. Approval retries return an existing Executed proposal without replaying posting or verification; pending/failed verification is handled through the recovery queue.
