using System.Text.Json;
using bams.server.DTOs.Customers;
using bams.server.Models.Customers;

namespace bams.server.Services.Interfaces;

public sealed class NrcService : INrcService
{
    private readonly IReadOnlyList<NrcTownshipRecord> _records;

    public NrcService(IWebHostEnvironment environment)
    {
        var filePath = Path.Combine(
            environment.ContentRootPath,
            "Data",
            "Nrc",
            "full_nrc_dataset.json");

        Console.WriteLine($"NRC File Path: {filePath}");
        Console.WriteLine($"File Exists: {File.Exists(filePath)}");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "NRC township dataset was not found.",
                filePath);
        }

        var json = File.ReadAllText(filePath);
        Console.WriteLine($"NRC dataset loaded from: {json.Length}");

        _records = JsonSerializer.Deserialize<
            List<NrcTownshipRecord>>(json)
            ?? throw new InvalidOperationException(
                "Failed to load NRC township dataset.");
    }

    public IReadOnlyList<NrcStateDto> GetStates()
    {
        return _records
            .GroupBy(statecode => statecode.StateCode)
            .Select(group => group.First())
            // .OrderBy(statecode => int.Parse(statecode.StateCode))
            .OrderBy(x =>
    int.TryParse(x.StateCode, out var code)
        ? code
        : int.MaxValue)
            .Select(statecode => new NrcStateDto(
                statecode.StateCode,
                statecode.StateEn,
                statecode.StateMm))
            .ToList();
    }

    public IReadOnlyList<NrcTownshipDto> GetTownshipsByState(
        string stateCode)
    {
        return _records
            .Where(statecode => statecode.StateCode == stateCode)
            .OrderBy(statecode => statecode.TownshipEn)
            .Select(statecode => new NrcTownshipDto(
                statecode.TownshipCodeEn,
                statecode.TownshipEn,
                statecode.TownshipMm))
            .ToList();
    }
}