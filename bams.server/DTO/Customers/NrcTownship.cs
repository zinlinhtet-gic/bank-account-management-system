namespace bams.server.DTOs.Customers;

public sealed record NrcTownshipDto(
    string TownshipCode,
    string TownshipEn,
    string TownshipMm
);