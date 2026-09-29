using bams.server.Constants;
using bams.server.DTO.Products;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/interest-rate-rules")]
public sealed class InterestRateRulesController : ControllerBase
{
    private readonly IInterestRateRuleService _interestRateRuleService;

    public InterestRateRulesController(IInterestRateRuleService interestRateRuleService)
    {
        _interestRateRuleService = interestRateRuleService;
    }

    /// <summary>
    /// Gets active interest rules currently effective for an account type.
    /// </summary>
    [HttpGet]
    [RequirePermission(SecurityConstants.AccountManagement)]
    public async Task<ActionResult<IReadOnlyList<InterestRateRuleResponse>>> GetAvailableRulesAsync(
        [FromQuery] long accountTypeId,
        CancellationToken cancellationToken)
    {
        var rules = await _interestRateRuleService.GetAvailableRulesAsync(
            accountTypeId,
            cancellationToken);

        return Ok(rules);
    }
}
