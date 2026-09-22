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
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
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

        return Created($"api/customers/{customer.Id}", response);
    }
}
