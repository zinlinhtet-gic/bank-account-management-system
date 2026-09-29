namespace bams.desktop.DTOs.Accounts;

public sealed record OwnedAccountOptionResponse(
    long Id,
    string AccountNo,
    string AccountTypeCode,
    string Status,
    string AccountTypeName)
{
    public string DisplayName => $"{AccountTypeName} · {AccountNo}";
}
