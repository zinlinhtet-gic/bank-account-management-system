using bams.server.Constants;
using bams.server.DTO.Common;
using bams.server.DTO.Configuration;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

/// <summary>
/// Fee rules. Every action requires the <c>configuration</c> permission. No delete.
/// </summary>
[ApiController]
[Route("api/fee-rates")]
[RequirePermission(SecurityConstants.Configuration)]
public sealed class FeeRatesController : ControllerBase
{
    private readonly IFeeRateService _feeRateService;

    public FeeRatesController(IFeeRateService feeRateService)
    {
        _feeRateService = feeRateService;
    }

    /// <summary>
    /// Gets all fee rules.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<FeeRuleResponse>>>> GetFeeRulesAsync(
        CancellationToken cancellationToken)
    {
        var rules = await _feeRateService.GetFeeRulesAsync(cancellationToken);

        return Ok(ApiMessageResponse<IReadOnlyList<FeeRuleResponse>>.FromCode(MessageCode.Success, rules));
    }

    /// <summary>
    /// Gets the account types selectable in the fee rule form.
    /// </summary>
    [HttpGet("account-types")]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<AccountTypeOptionResponse>>>> GetAccountTypeOptionsAsync(
        CancellationToken cancellationToken)
    {
        var accountTypes = await _feeRateService.GetAccountTypeOptionsAsync(cancellationToken);

        return Ok(ApiMessageResponse<IReadOnlyList<AccountTypeOptionResponse>>.FromCode(MessageCode.Success, accountTypes));
    }

    /// <summary>
    /// Creates a new fee rule.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiMessageResponse<FeeRuleResponse>>> CreateFeeRuleAsync(
        CreateFeeRuleRequest request,
        CancellationToken cancellationToken)
    {
        var rule = await _feeRateService.CreateFeeRuleAsync(request, cancellationToken);
        var response = ApiMessageResponse<FeeRuleResponse>.FromCode(
            MessageCode.FeeRuleCreatedSuccessfully,
            rule);

        return Ok(response);
    }

    /// <summary>
    /// Updates an existing fee rule.
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiMessageResponse<FeeRuleResponse>>> UpdateFeeRuleAsync(
        long id,
        UpdateFeeRuleRequest request,
        CancellationToken cancellationToken)
    {
        var rule = await _feeRateService.UpdateFeeRuleAsync(id, request, cancellationToken);
        var response = ApiMessageResponse<FeeRuleResponse>.FromCode(
            MessageCode.FeeRuleUpdatedSuccessfully,
            rule);

        return Ok(response);
    }
}
