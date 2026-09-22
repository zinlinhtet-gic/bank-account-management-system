using bams.server.Constants;
using bams.server.Data;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class CustomerNumberGenerator
{
    private readonly ApplicationDbContext _dbContext;

    public CustomerNumberGenerator(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;

    }

    public async Task<string> GenerateAsync(
        CancellationToken cancellationToken)
    {
        var lastCustomerNo = await _dbContext.Customers
            .AsNoTracking()
            .Where(c =>
                c.CustomerNo.StartsWith(
                    CustomerConstants.CustomerNumberPrefix))
            .OrderByDescending(c => c.Id)
            .Select(c => c.CustomerNo)
            .FirstOrDefaultAsync(cancellationToken);

        var nextNumber = 1;

        if (!string.IsNullOrWhiteSpace(lastCustomerNo))
        {
            var numberPart = lastCustomerNo[
                CustomerConstants.CustomerNumberPrefix.Length..];

            if (int.TryParse(numberPart, out var currentNumber))
            {
                nextNumber = currentNumber + 1;
            }
        }

        return $"{CustomerConstants.CustomerNumberPrefix}{nextNumber:D8}";
    }
}