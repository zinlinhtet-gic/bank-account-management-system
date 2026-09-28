using bams.server.Constants;
using bams.server.DTO.Accounts;
using bams.server.DTO.Common;
using bams.server.DTO.Customers;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerCreationService _customerCreationService;

    public CustomersController(ICustomerCreationService customerCreationService)
    {
        _customerCreationService = customerCreationService;
    }

    /// <summary>Creates and persists a customer record for account opening.</summary>
    [HttpPost]
    [RequirePermission(SecurityConstants.CustomerManagement)]
    public async Task<ActionResult<ApiMessageResponse<CustomerLookupResponse>>> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerCreationService.CreateCustomerAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiMessageResponse<CustomerLookupResponse>.FromCode(MessageCode.CustomerCreatedSuccessfully, customer));
    }
}
