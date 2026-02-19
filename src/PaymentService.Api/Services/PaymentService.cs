using PaymentService.Api.Domain;
using PaymentService.Api.Models;
using PaymentService.Api.Processors;
using PaymentService.Api.Repositories;

namespace PaymentService.Api.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _repository;
    private readonly IPaymentProcessor _processor;

    public PaymentService(IPaymentRepository repository, IPaymentProcessor processor)
    {
        _repository = repository;
        _processor = processor;
    }

    public async Task<(PaymentResponse Response, bool IsExisting)> CreatePaymentAsync(CreatePaymentRequest request)
    {
        // Idempotency check
        var existing = await _repository.GetByReferenceIdAsync(request.ReferenceId);
        if (existing != null)
            return (MapToResponse(existing), true);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            ReferenceId = request.ReferenceId,
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            Status = PaymentStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var created = await _repository.CreateAsync(payment);
        if (!created)
        {
            // Race condition: another request created it
            var raceExisting = await _repository.GetByReferenceIdAsync(request.ReferenceId);
            if (raceExisting != null)
                return (MapToResponse(raceExisting), true);
        }

        // Process payment
        var result = await _processor.ProcessAsync(payment);
        await _repository.UpdateStatusAsync(payment.Id, result.Status, result.Reason);

        payment.Status = result.Status;
        payment.FailureReason = result.Reason;

        return (MapToResponse(payment), false);
    }

    public async Task<PaymentResponse?> GetPaymentByIdAsync(Guid id)
    {
        var payment = await _repository.GetByIdAsync(id);
        return payment == null ? null : MapToResponse(payment);
    }

    public async Task<PaymentResponse?> GetPaymentByReferenceIdAsync(string referenceId)
    {
        var payment = await _repository.GetByReferenceIdAsync(referenceId);
        return payment == null ? null : MapToResponse(payment);
    }

    private static PaymentResponse MapToResponse(Payment payment) => new()
    {
        Id = payment.Id,
        ReferenceId = payment.ReferenceId,
        Amount = payment.Amount,
        Currency = payment.Currency,
        Status = payment.Status.ToString(),
        FailureReason = payment.FailureReason,
        CreatedAtUtc = payment.CreatedAtUtc
    };
}
