using bams.server.Data;
using bams.server.DTO.Configuration;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class OtherBankService : IOtherBankService
{
    private readonly ApplicationDbContext _dbContext;

    public OtherBankService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// Gets all other banks using a read-only database query.
    public async Task<IReadOnlyList<OtherBankResponse>> GetOtherBanksAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.OtherBanks
            .AsNoTracking()
            .OrderBy(bank => bank.BankName)
            .Select(bank => new OtherBankResponse(
                bank.Id,
                bank.BankCode,
                bank.BankName,
                bank.SwiftCode,
                bank.Status))
            .ToListAsync(cancellationToken);
    }
}
