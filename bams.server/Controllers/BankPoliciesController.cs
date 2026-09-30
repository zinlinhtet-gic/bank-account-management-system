using bams.server.Constants;
using bams.server.DTO.Common;
using bams.server.DTO.Configuration;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

/// <summary>
/// Bank policies (account type product terms). Every action requires the <c>configuration</c> permission.
/// No delete: an account type in use elsewhere cannot be safely removed.
/// </summary>
[ApiController]
[Route("api/bank-policies")]
[RequirePermission(SecurityConstants.Configuration)]
public sealed class BankPoliciesController : ControllerBase
{
    private readonly IBankPolicyService _bankPolicyService;

    public BankPoliciesController(IBankPolicyService bankPolicyService)
    {
        _bankPolicyService = bankPolicyService;
    }

    /// <summary>
    /// Gets one page of bank policies, 10 per page.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiMessageResponse<PagedResponse<BankPolicyResponse>>>> GetBankPoliciesAsync(
        [FromQuery] int page,
        CancellationToken cancellationToken)
    {
        var policies = await _bankPolicyService.GetBankPoliciesAsync(page, cancellationToken);

        return Ok(ApiMessageResponse<PagedResponse<BankPolicyResponse>>.FromCode(MessageCode.Success, policies));
    }

    /// <summary>
    /// Creates a new bank policy.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiMessageResponse<BankPolicyResponse>>> CreateBankPolicyAsync(
        CreateBankPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var policy = await _bankPolicyService.CreateBankPolicyAsync(request, cancellationToken);
        var response = ApiMessageResponse<BankPolicyResponse>.FromCode(
            MessageCode.BankPolicyCreatedSuccessfully,
            policy);

        return Ok(response);
    }

    /// <summary>
    /// Updates an existing bank policy.
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiMessageResponse<BankPolicyResponse>>> UpdateBankPolicyAsync(
        long id,
        UpdateBankPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var policy = await _bankPolicyService.UpdateBankPolicyAsync(id, request, cancellationToken);
        var response = ApiMessageResponse<BankPolicyResponse>.FromCode(
            MessageCode.BankPolicyUpdatedSuccessfully,
            policy);

        return Ok(response);
    }
}
