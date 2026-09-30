using bams.server.Constants;
using bams.server.DTO.Common;
using bams.server.DTO.Configuration;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;


/// Interest rate rules
[ApiController]
[Route("api/interest-rates")]
[RequirePermission(SecurityConstants.Configuration)]
public sealed class InterestRatesController : ControllerBase
{
    private readonly IInterestRateService _interestRateService;

    public InterestRatesController(IInterestRateService interestRateService)
    {
        _interestRateService = interestRateService;
    }

    /// Gets one page of interest rate rules, 10 per page.
    [HttpGet]
    public async Task<ActionResult<ApiMessageResponse<PagedResponse<InterestRateResponse>>>> GetInterestRatesAsync(
        [FromQuery] int page,
        CancellationToken cancellationToken)
    {
        var rates = await _interestRateService.GetInterestRatesAsync(page, cancellationToken);

        return Ok(ApiMessageResponse<PagedResponse<InterestRateResponse>>.FromCode(MessageCode.Success, rates));
    }

    /// <summary>
    /// Gets the account types selectable in the interest rate form.
    /// </summary>
    [HttpGet("account-types")]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<AccountTypeOptionResponse>>>> GetAccountTypeOptionsAsync(
        CancellationToken cancellationToken)
    {
        var accountTypes = await _interestRateService.GetAccountTypeOptionsAsync(cancellationToken);

        return Ok(ApiMessageResponse<IReadOnlyList<AccountTypeOptionResponse>>.FromCode(MessageCode.Success, accountTypes));
    }

    /// Creates a new interest rate rule.
    [HttpPost]
    public async Task<ActionResult<ApiMessageResponse<InterestRateResponse>>> CreateInterestRateAsync(
        CreateInterestRateRequest request,
        CancellationToken cancellationToken)
    {
        var rate = await _interestRateService.CreateInterestRateAsync(request, cancellationToken);
        var response = ApiMessageResponse<InterestRateResponse>.FromCode(
            MessageCode.InterestRateCreatedSuccessfully,
            rate);

        return Ok(response);
    }

    /// Updates an existing interest rate rule.
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiMessageResponse<InterestRateResponse>>> UpdateInterestRateAsync(
        long id,
        UpdateInterestRateRequest request,
        CancellationToken cancellationToken)
    {
        var rate = await _interestRateService.UpdateInterestRateAsync(id, request, cancellationToken);
        var response = ApiMessageResponse<InterestRateResponse>.FromCode(
            MessageCode.InterestRateUpdatedSuccessfully,
            rate);

        return Ok(response);
    }
}
