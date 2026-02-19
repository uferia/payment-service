using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;
using PaymentService.Api.Models;
using PaymentService.Api.Services;

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

        try
        {
            var (response, isExisting) = await _paymentService.CreatePaymentAsync(request);

            if (response.Status == "Rejected")
            {
                return UnprocessableEntity(new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Payment Rejected",
                    Status = 422,
                    Detail = response.FailureReason ?? "Payment was rejected.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (response.Status == "Failed")
            {
                return StatusCode(503, new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.4",
                    Title = "Service Unavailable",
                    Status = 503,
                    Detail = "Payment processing is temporarily unavailable. Please retry later.",
                    TraceId = HttpContext.TraceIdentifier,
                    RetryAfterSeconds = 30
                });
            }

            if (isExisting)
                return Ok(response);

            return CreatedAtAction(nameof(GetPaymentById), new { id = response.Id }, response);
        }
        catch (BrokenCircuitException)
        {
            return StatusCode(503, new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.4",
                Title = "Service Unavailable",
                Status = 503,
                Detail = "Payment processing is temporarily unavailable. Please retry later.",
                TraceId = HttpContext.TraceIdentifier,
                RetryAfterSeconds = 30
            });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPaymentById(Guid id)
    {
        var payment = await _paymentService.GetPaymentByIdAsync(id);
        if (payment == null)
            return NotFound();
        return Ok(payment);
    }

    [HttpGet("reference/{referenceId}")]
    public async Task<IActionResult> GetPaymentByReference(string referenceId)
    {
        var payment = await _paymentService.GetPaymentByReferenceIdAsync(referenceId);
        if (payment == null)
            return NotFound();
        return Ok(payment);
    }
}
