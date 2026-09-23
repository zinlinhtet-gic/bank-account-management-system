using bams.server.DTO.Common;
using bams.server.DTO.Customers;
using bams.server.Messages;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private const string GetCustomerByIdRouteName = "GetCustomerById";

    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>
    /// Gets a page of customer summaries (10 per page), optionally filtered by customer
    /// number, name, KYC status, status, or risk level.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CustomerSummaryResponse>>> GetCustomersAsync(
        [FromQuery] GetCustomersRequest request,
        CancellationToken cancellationToken)
    {
        var customers = await _customerService.GetCustomersAsync(request, cancellationToken);

        return Ok(customers);
    }

    /// <summary>
    /// Gets a single customer, including its documents, by its unique identifier.
    /// </summary>
    [HttpGet("{id:long}", Name = GetCustomerByIdRouteName)]
    public async Task<ActionResult<CustomerResponse>> GetCustomerByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var customer = await _customerService.GetCustomerByIdAsync(id, cancellationToken);

        return Ok(customer);
    }

    /// <summary>
    /// Creates a new customer from the supplied API request contract.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiMessageResponse<CustomerResponse>>> CreateCustomerAsync(
        [FromForm] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerService.CreateCustomerAsync(request, cancellationToken);
        var response = ApiMessageResponse<CustomerResponse>.FromCode(
            MessageCode.CustomerCreatedSuccessfully,
            customer);

        return CreatedAtRoute(
            GetCustomerByIdRouteName,
            new { id = customer.Id },
            response);
    }

    /// <summary>
    /// Partially updates a customer's fields and documents. Only supplied properties change;
    /// document entries with an Id edit an existing document (optionally replacing its file),
    /// entries without one add a new document. Multipart form data, so new files can be attached.
    /// </summary>
    [HttpPatch("{id:long}")]
    public async Task<ActionResult<ApiMessageResponse<CustomerResponse>>> UpdateCustomerAsync(
        long id,
        [FromForm] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerService.UpdateCustomerAsync(id, request, cancellationToken);
        var response = ApiMessageResponse<CustomerResponse>.FromCode(
            MessageCode.CustomerUpdatedSuccessfully,
            customer);

        return Ok(response);
    }

    /// <summary>
    /// Records a KYC review decision for a customer. Approving (Verified) stamps every one
    /// of the customer's documents as verified; rejecting only changes the customer's KycStatus.
    /// </summary>
    [HttpPost("{id:long}/kyc-review")]
    public async Task<ActionResult<ApiMessageResponse<CustomerResponse>>> ReviewCustomerKycAsync(
        long id,
        [FromBody] ReviewCustomerKycRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerService.ReviewCustomerKycAsync(id, request, cancellationToken);
        var response = ApiMessageResponse<CustomerResponse>.FromCode(
            MessageCode.CustomerKycReviewedSuccessfully,
            customer);

        return Ok(response);
    }
}
