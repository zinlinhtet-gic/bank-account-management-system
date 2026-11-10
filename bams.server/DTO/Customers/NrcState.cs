namespace bams.server.DTOs.Customers;

public sealed record NrcStateDto(
    string StateCode,
    string StateEn,
    string StateMm
);