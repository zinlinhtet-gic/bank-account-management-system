namespace bams.desktop.DTOs.Customers;

/// <summary>One NRC state/region, from <c>GET /api/nrc/states</c>.</summary>
public sealed record NrcStateResponse(string StateCode)
{
    // ComboBox shows ToString() when no template is set.
    public string DisplayName => $"{StateCode}";

    public override string ToString() => DisplayName;
}

/// <summary>One township within a state, from <c>GET /api/nrc/states/{stateCode}/townships</c>.</summary>
public sealed record NrcTownshipResponse(string TownshipCode)
{
    public string DisplayName => $"{TownshipCode}";

    public override string ToString() => DisplayName;
}
