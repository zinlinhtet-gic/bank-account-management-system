using bams.server.Constants;
using bams.server.DTO.Common;
using bams.server.DTO.Configuration;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/other-banks")]
public sealed class OtherBanksController : ControllerBase
{
    private readonly IOtherBankService _otherBankService;

    public OtherBanksController(IOtherBankService otherBankService)
    {
        _otherBankService = otherBankService;
    }

    /// Gets one page of other banks, 10 per page.
    [HttpGet]
    [RequirePermission(SecurityConstants.Configuration)]
    public async Task<ActionResult<ApiMessageResponse<PagedResponse<OtherBankResponse>>>> GetOtherBanksAsync(
        [FromQuery] int page,
        CancellationToken cancellationToken)
    {
        var otherBanks = await _otherBankService.GetOtherBanksAsync(page, cancellationToken);

        return Ok(ApiMessageResponse<PagedResponse<OtherBankResponse>>.FromCode(MessageCode.Success, otherBanks));
    }
}
