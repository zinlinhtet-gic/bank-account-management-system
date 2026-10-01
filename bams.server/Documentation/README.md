# Server Documentation

Use the documents in this order:

1. [Architecture](Architecture.md) — application layers, security, persistence, and runtime responsibilities.
2. [API Contracts](ApiContracts.md) — human-readable behavior of the currently exposed account endpoints.
3. [Business Rules](BusinessRules.md) — domain invariants enforced by services.
4. [Message Codes](MessageCodes.md) — stable server and desktop-client message-code registry.
5. [OpenAPI specification](AccountsApi.openapi.yaml) — machine-readable contract for `AccountsController` and `AccountTypesController`.

## Sources of truth

- Controllers and DTOs define the exposed HTTP surface.
- Services and model configuration define business and persistence behavior.
- `Messages/MessageCode.cs` defines server message-code values.
- `AccountsApi.openapi.yaml` must be updated whenever either documented controller changes.

Documentation should describe current behavior only. Planned or retired endpoints belong in issues or design proposals, not in these contracts.
