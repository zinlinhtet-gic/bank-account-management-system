# Server documentation

This folder describes the current `bams.server` implementation. Start with the topic you need:

| Document | Use it for |
| --- | --- |
| [Architecture](Architecture.md) | Runtime flow, project boundaries, persistence, security, and background work. |
| [API contracts](ApiContracts.md) | HTTP routes, request behavior, pagination, envelopes, and errors. |
| [Business rules](BusinessRules.md) | Domain invariants and lifecycle rules enforced by services. |
| [Message codes](MessageCodes.md) | Stable server codes and client-only codes. |
| [OpenAPI contract](AccountsApi.openapi.yaml) | Machine-readable API schema. |

## Sources of truth

- Controllers, request/response DTOs, authorization attributes, and exception handling define HTTP behavior.
- Services, entities, EF Core configurations, and constants define business and persistence behavior.
- `Messages/MessageCode.cs` defines server code names and numeric values; `Messages/MessageCatalog.cs` defines default text.
- The OpenAPI file is a maintained contract, not a substitute for checking implementation. Confirm its declared coverage before relying on it for routes outside accounts.

Keep these pages focused on behavior present in the code. Update the relevant page and OpenAPI operations when implementation changes; remove obsolete descriptions instead of preserving historical behavior inline.
