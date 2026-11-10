namespace bams.server.Models.Customers;

using System.Text.Json.Serialization;

public sealed class NrcTownshipRecord
{
    [JsonPropertyName("township")]
    public string Township { get; set; } = string.Empty;

    [JsonPropertyName("township_en")]
    public string TownshipEn { get; set; } = string.Empty;

    [JsonPropertyName("township_mm")]
    public string TownshipMm { get; set; } = string.Empty;

    [JsonPropertyName("township_code_en")]
    public string TownshipCodeEn { get; set; } = string.Empty;

    [JsonPropertyName("township_code_mm")]
    public string TownshipCodeMm { get; set; } = string.Empty;

    [JsonPropertyName("state_code")]
    public string StateCode { get; set; } = string.Empty;

    [JsonPropertyName("state_en")]
    public string StateEn { get; set; } = string.Empty;

    [JsonPropertyName("state_mm")]
    public string StateMm { get; set; } = string.Empty;
}