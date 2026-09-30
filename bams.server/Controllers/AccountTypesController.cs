using bams.server.DTO.Common;
using bams.server.Messages;
using bams.server.DTO.Products;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/account-types")]
public sealed class AccountTypesController : ControllerBase
{
    private readonly IAccountTypeService _accountTypeService;

    public AccountTypesController(IAccountTypeService accountTypeService)
    {
        _accountTypeService = accountTypeService;
    }

    /// <summary>
    /// Gets all active account products available for account opening.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<AccountTypeResponse>>>> GetAvailableAccountTypesAsync(
        CancellationToken cancellationToken)
    {
        var accountTypes = await _accountTypeService.GetAvailableAccountTypesAsync(cancellationToken);

        return Ok(ApiMessageResponse<IReadOnlyList<AccountTypeResponse>>.FromCode(MessageCode.Success, accountTypes));
    }
}
