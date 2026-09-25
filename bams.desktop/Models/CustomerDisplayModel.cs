using bams.desktop.Constants;
using bams.desktop.DTOs.Customers;
using bams.desktop.Utils;

namespace bams.desktop.Models;

/// <summary>
/// One row of the Customer List table, shaped for binding.
/// </summary>
/// <param name="No">The row's position across every page (e.g. 11 for row 1 of page 2 at page size 10).</param>
public sealed record CustomerDisplayModel(
    int No,
    long Id,
    string CustomerNo,
    string CustomerType,
    string FullName,
    string Phone,
    string Email,
    string KycStatus,
    string RiskLevel,
    string Status,
    string CreatedAt)
{
    /// <summary>
    /// Builds a row from the server list item.
    /// </summary>
    public static CustomerDisplayModel FromResponse(CustomerSummaryResponse customer, int rowNumber)
    {
        return new CustomerDisplayModel(
            rowNumber,
            customer.Id,
            customer.CustomerNo,
            customer.CustomerType.ToString(),
            customer.FullName,
            string.IsNullOrWhiteSpace(customer.Phone) ? DisplayFormats.EmptyValue : customer.Phone,
            string.IsNullOrWhiteSpace(customer.Email) ? DisplayFormats.EmptyValue : customer.Email,
            customer.KycStatus.ToString(),
            customer.RiskLevel.ToString(),
            customer.Status,
            DateTimeDisplay.ToLocal(customer.CreatedAt).ToString(DisplayFormats.Date));
    }
}
