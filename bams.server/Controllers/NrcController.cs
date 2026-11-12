using bams.server.DTO.Common;
using bams.server.DTOs.Customers;
using bams.server.Messages;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/nrc")]
public sealed class NrcController : ControllerBase
{
    private readonly INrcService _nrcService;

    public NrcController(INrcService nrcService)
    {
        _nrcService = nrcService;
    }

    // Every other endpoint in this project returns the ApiMessageResponse<T> envelope (see
    // UsersController, CustomerService, etc.); the client's ApiClient.ReadResponseAsync always
    // unwraps that envelope's Data property, so a bare array here would fail client-side deserialization.
    [HttpGet("states")]
    public ActionResult<ApiMessageResponse<IReadOnlyList<NrcStateDto>>> GetStates()
    {
        return Ok(ApiMessageResponse<IReadOnlyList<NrcStateDto>>.FromCode(MessageCode.Success, _nrcService.GetStates()));
    }

    [HttpGet("states/{stateCode}/townships")]
    public ActionResult<ApiMessageResponse<IReadOnlyList<NrcTownshipDto>>> GetTownships(string stateCode)
    {
        var states = _nrcService.GetStates();

        if (!states.Any(statecode => statecode.StateCode == stateCode))
        {
            return NotFound();
        }

        return Ok(ApiMessageResponse<IReadOnlyList<NrcTownshipDto>>.FromCode(
            MessageCode.Success, _nrcService.GetTownshipsByState(stateCode)));
    }
}