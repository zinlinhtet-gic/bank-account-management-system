using bams.server.Constants;
using bams.server.DTO.Accounting;
using bams.server.DTO.Common;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/cash-operations/expenses")]
public sealed class BankExpensesController(BankExpenseService expenses) : ControllerBase
{
    [HttpGet]
    [RequirePermission(SecurityConstants.CashOperations, SecurityConstants.EndOfDayApproval, SecurityConstants.Audit)]
    public async Task<IActionResult> List([FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok(ApiMessageResponse<IReadOnlyList<BankExpenseResponse>>.FromCode(MessageCode.Success, await expenses.ListAsync(date, ct)));

    [HttpPost]
    [RequirePermission(SecurityConstants.CashOperations, SecurityConstants.EndOfDayApproval)]
    public async Task<IActionResult> Submit(SubmitBankExpenseRequest request, CancellationToken ct) =>
        Ok(ApiMessageResponse<BankExpenseResponse>.FromCode(MessageCode.Success, await expenses.SubmitAsync(request, ct)));

    [HttpPost("{id:long}/review")]
    [RequirePermission(SecurityConstants.EndOfDayApproval)]
    public async Task<IActionResult> Review(long id, ReviewBankExpenseRequest request, CancellationToken ct) =>
        Ok(ApiMessageResponse<BankExpenseResponse>.FromCode(MessageCode.Success, await expenses.ReviewAsync(id, request, ct)));

    [HttpGet("{id:long}/receipt")]
    [RequirePermission(SecurityConstants.CashOperations, SecurityConstants.EndOfDayApproval, SecurityConstants.Audit)]
    public async Task<IActionResult> Receipt(long id, CancellationToken ct)
    {
        (byte[] Data, string Name, string ContentType) receipt = await expenses.ReceiptAsync(id, ct);
        return File(receipt.Data, receipt.ContentType, receipt.Name);
    }
}
