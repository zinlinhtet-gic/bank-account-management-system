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
    /// Gets all customer summaries.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerSummaryResponse>>> GetCustomersAsync(
        CancellationToken cancellationToken)
    {
        var customers = await _customerService.GetCustomersAsync(cancellationToken);

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
}
