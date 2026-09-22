using System.Globalization;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Exceptions;
using bams.server.Mapping;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.Customers;
using bams.server.Models.Products;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class AccountTransactionService : IAccountTransactionService
{
    private readonly ApplicationDbContext _dbContext;

    public AccountTransactionService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task RecordAccountOpeningTransactionAsync(
        Account account,
        decimal openingBalance,
        DateTime currentDateTime,
        CancellationToken cancellationToken)
    {
        // Create a new account transaction for the account opening
    }
}
