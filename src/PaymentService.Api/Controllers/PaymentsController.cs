using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Models;
using PaymentService.Application.Services;
using PaymentService.Core.Results;

namespace PaymentService.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IValidator<CreatePaymentRequest> _validator;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentService paymentService,
        IValidator<CreatePaymentRequest> validator,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _validator = validator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var result = await _paymentService.CreatePaymentAsync(request);

        if (result.IsFailure)
            return ToErrorResponse(result.Error!);

        var (payment, isExisting) = result.Value;

        if (isExisting)
            return Ok(payment);

        return CreatedAtAction(nameof(GetPaymentById), new { id = payment.Id }, payment);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPaymentById(Guid id)
    {
        var result = await _paymentService.GetPaymentByIdAsync(id);
        if (result.IsFailure)
            return NotFound();
        return Ok(result.Value);
    }

    [HttpGet("reference/{referenceId}")]
    public async Task<IActionResult> GetPaymentByReference(string referenceId)
    {
        var result = await _paymentService.GetPaymentByReferenceIdAsync(referenceId);
        if (result.IsFailure)
            return NotFound();
        return Ok(result.Value);
    }

    private ObjectResult ToErrorResponse(Error error) =>
        StatusCode(error.StatusCode, new ErrorResponse
        {
            Type = error.RfcType,
            Title = error.Title,
            Status = error.StatusCode,
            Detail = error.Detail,
            TraceId = HttpContext.TraceIdentifier,
            RetryAfterSeconds = error.RetryAfterSeconds
        });
}
