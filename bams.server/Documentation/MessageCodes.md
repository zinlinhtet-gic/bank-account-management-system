# Message codes

Message codes are stable client/server contracts. Clients must branch on the numeric code, not display text. Keep a code's name and numeric value stable after release; add new codes instead of reusing retired values.

## Canonical registries

Account document reuse adds `3133 / AccountDocumentSourceInvalid` (exactly one upload or source reference is required) and `3134 / ReusableAccountDocumentUnavailable` (refresh options because the source no longer satisfies ownership, type, expiry, or file requirements). Both are validation failures and are mirrored in the desktop client.

- Server names and values: [`Messages/MessageCode.cs`](../Messages/MessageCode.cs).
- Server default message text: [`Messages/MessageCatalog.cs`](../Messages/MessageCatalog.cs).
- Desktop mirror and client-only codes: [`bams.desktop/Utils/MessageCode.cs`](../../bams.desktop/Utils/MessageCode.cs).

The enums are the complete registry. This page intentionally does not duplicate the enum tables, which previously drifted and contained conflicting entries. HTTP status mappings are defined by the exception/response handling code; a numeric range alone does not determine the status.

## Ranges

| Range | Category |
| --- | --- |
| 1000–1999 | Success |
| 3000–3999 | Validation |
| 4000–4099 | Authentication |
| 4100–4199 | Authorization |
| 4200–4299 | Not found |
| 4300–4399 | Conflict |
| 4400–4499 | Business rule |
| 5000–5999 | System or infrastructure |
| 6000 and above | Desktop client only; never returned by the server |

## Adding or changing a code

1. Search both server and desktop enums for the numeric value and code name before assigning it. Feature ranges are planning guidance, not proof that a value is unused.
2. Add the server enum value and catalog text when the server returns the code. Add the matching desktop enum value and user-facing handling where needed.
3. Map the server code to the correct HTTP status through the exception/response handling path.
4. Update API documentation when callers can observe the new behavior. Do not make clients parse the message string.


The reconciliation proposal workflow adds stable business-rule codes 4490 through 4493: `CorrectionProposalStale`, `CorrectionAlreadyApplied`, `CorrectionLegacyResubmit`, and `CorrectionEvidenceRequired`.

EOD uses the existing stable error codes: ReconciliationBlocked for unresolved normal-close readiness, EndOfDayApprovalRequired for approval rules, and BusinessDateTransitionConflict for invalid date transitions. Force-close bypass does not suppress infrastructure errors. Audit batch/item statuses (CompletedWithExceptions, Exception, Waived) are persisted outcomes rather than new message codes.
