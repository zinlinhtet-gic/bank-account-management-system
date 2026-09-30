using bams.server.Data;
using bams.server.DTO.Common;
using bams.server.DTO.Configuration;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class OtherBankService : IOtherBankService
{
    private const int PageSize = 10;

    private readonly ApplicationDbContext _dbContext;

    public OtherBankService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// Gets one page of other banks, 10 per page.
    public async Task<PagedResponse<OtherBankResponse>> GetOtherBanksAsync(
        int page,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);

        var query = _dbContext.OtherBanks
            .AsNoTracking()
            .OrderBy(bank => bank.BankName);

        var totalCount = await query.CountAsync(cancellationToken);
        var banks = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(bank => new OtherBankResponse(
                bank.Id,
                bank.BankCode,
                bank.BankName,
                bank.SwiftCode,
                bank.Status))
            .ToListAsync(cancellationToken);

        return new PagedResponse<OtherBankResponse>(banks, page, PageSize, totalCount);
    }
}
