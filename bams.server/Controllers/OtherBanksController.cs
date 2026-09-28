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

    /// Gets all other banks. 
    [HttpGet]
    [RequirePermission(SecurityConstants.Configuration)]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<OtherBankResponse>>>> GetOtherBanksAsync(
        CancellationToken cancellationToken)
    {
        var otherBanks = await _otherBankService.GetOtherBanksAsync(cancellationToken);

        return Ok(ApiMessageResponse<IReadOnlyList<OtherBankResponse>>.FromCode(MessageCode.Success, otherBanks));
    }
}
