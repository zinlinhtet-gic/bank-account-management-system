using bams.server.Constants;
using bams.server.DTO.Common;
using bams.server.DTO.Jobs;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/operations/scheduled-jobs")]
[RequirePermission(SecurityConstants.ScheduledJobManagement)]
public sealed class ScheduledJobsController : ControllerBase
{
    private readonly IJobsOperationService _jobsOperationService;

    public ScheduledJobsController(IJobsOperationService jobsOperationService)
    {
        _jobsOperationService = jobsOperationService;
    }

    /// <summary>Lists scheduled occurrences that exhausted automatic retries and need manager attention.</summary>
    [HttpGet("failures")]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<FailedScheduledJobResponse>>>> GetFailuresAsync(
        CancellationToken cancellationToken)
    {
        var failures = await _jobsOperationService.GetFinalFailuresAsync(cancellationToken);
        return Ok(ApiMessageResponse<IReadOnlyList<FailedScheduledJobResponse>>.FromCode(MessageCode.Success, failures));
    }

    /// <summary>Requests another execution of the failed occurrence while preserving its original scheduled date.</summary>
    [HttpPost("executions/{executionId:long}/retry")]
    public async Task<ActionResult<ApiMessageResponse<ScheduledJobRetryResponse>>> RetryAsync(
        long executionId, CancellationToken cancellationToken)
    {
        var result = await _jobsOperationService.RequestManualRetryAsync(executionId, cancellationToken);
        return Ok(ApiMessageResponse<ScheduledJobRetryResponse>.FromCode(MessageCode.Success, result));
    }
}
