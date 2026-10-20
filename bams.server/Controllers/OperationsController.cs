using bams.server.Constants;
using bams.server.DTO.Common;
using bams.server.DTO.Operations;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

/// Operations: per-customer views of interest, fees and fixed-deposit maturity.

[ApiController]
[Route("api/operations")]
[RequirePermission(SecurityConstants.Operation)]
public sealed class OperationsController : ControllerBase
{
    private readonly IOperationQueryService _operationQueryService;

    public OperationsController(IOperationQueryService operationQueryService)
    {
        _operationQueryService = operationQueryService;
    }

    /// Lists each customer account's monthly interest accruals. Optional filters: search, status, from, to, page, pageSize.
    [HttpGet("interest")]
    public async Task<ActionResult<ApiMessageResponse<PagedResponse<InterestOperationResponse>>>> GetInterestOperationsAsync(
        [FromQuery] InterestOperationQuery query,
        CancellationToken cancellationToken)
    {
        var interest = await _operationQueryService.GetInterestOperationsAsync(query, cancellationToken);

        return Ok(ApiMessageResponse<PagedResponse<InterestOperationResponse>>.FromCode(MessageCode.Success, interest));
    }

    /// Lists each customer account's fees. Optional filters: search, feeType, status, from, to, page, pageSize.
    [HttpGet("fees")]
    public async Task<ActionResult<ApiMessageResponse<PagedResponse<FeeOperationResponse>>>> GetFeeOperationsAsync(
        [FromQuery] FeeOperationQuery query,
        CancellationToken cancellationToken)
    {
        var fees = await _operationQueryService.GetFeeOperationsAsync(query, cancellationToken);

        return Ok(ApiMessageResponse<PagedResponse<FeeOperationResponse>>.FromCode(MessageCode.Success, fees));
    }

    /// Lists each customer's fixed deposits by maturity date. Optional filters: search, status, from, to, page, pageSize.
    [HttpGet("fixed-deposit-maturity")]
    public async Task<ActionResult<ApiMessageResponse<PagedResponse<FixedDepositMaturityResponse>>>> GetFixedDepositMaturitiesAsync(
        [FromQuery] FixedDepositMaturityQuery query,
        CancellationToken cancellationToken)
    {
        var deposits = await _operationQueryService.GetFixedDepositMaturitiesAsync(query, cancellationToken);

        return Ok(ApiMessageResponse<PagedResponse<FixedDepositMaturityResponse>>.FromCode(MessageCode.Success, deposits));
    }

    /// Gets one interest accrual with its account, customer and linked transactions.
    [HttpGet("interest/{id:long}")]
    public async Task<ActionResult<ApiMessageResponse<InterestOperationDetailResponse>>> GetInterestOperationByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var interest = await _operationQueryService.GetInterestOperationByIdAsync(id, cancellationToken);

        return Ok(ApiMessageResponse<InterestOperationDetailResponse>.FromCode(MessageCode.Success, interest));
    }

    /// <summary>
    /// Gets one fee accrual with its account, customer, fee rule and linked transactions.
    /// </summary>
    [HttpGet("fees/{id:long}")]
    public async Task<ActionResult<ApiMessageResponse<FeeOperationDetailResponse>>> GetFeeOperationByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var fee = await _operationQueryService.GetFeeOperationByIdAsync(id, cancellationToken);

        return Ok(ApiMessageResponse<FeeOperationDetailResponse>.FromCode(MessageCode.Success, fee));
    }

    /// <summary>
    /// Gets one fixed deposit with its account, customer and month-by-month interest schedule.
    /// </summary>
    [HttpGet("fixed-deposit-maturity/{id:long}")]
    public async Task<ActionResult<ApiMessageResponse<FixedDepositMaturityDetailResponse>>> GetFixedDepositMaturityByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var deposit = await _operationQueryService.GetFixedDepositMaturityByIdAsync(id, cancellationToken);

        return Ok(ApiMessageResponse<FixedDepositMaturityDetailResponse>.FromCode(MessageCode.Success, deposit));
    }
}
