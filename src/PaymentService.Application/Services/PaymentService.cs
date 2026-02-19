using PaymentService.Application.Models;
using PaymentService.Core.Abstractions;
using PaymentService.Core.Domain;
using PaymentService.Core.Results;

namespace PaymentService.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _repository;
    private readonly IPaymentProcessor _processor;

    public PaymentService(IPaymentRepository repository, IPaymentProcessor processor)
    {
        _repository = repository;
        _processor = processor;
    }

    public async Task<Result<CreatePaymentResult>> CreatePaymentAsync(CreatePaymentRequest request)
    {
        var existing = await _repository.GetByReferenceIdAsync(request.ReferenceId);
        if (existing != null)
            return new CreatePaymentResult(MapToResponse(existing), true);

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
            var raceExisting = await _repository.GetByReferenceIdAsync(request.ReferenceId);
            if (raceExisting != null)
                return new CreatePaymentResult(MapToResponse(raceExisting), true);
        }

        var result = await _processor.ProcessAsync(payment);
        await _repository.UpdateStatusAsync(payment.Id, result.Status, result.Reason);

        if (result.Status == PaymentStatus.Rejected)
            return Error.PaymentRejected(result.Reason);

        if (result.Status == PaymentStatus.Failed)
            return Error.ServiceUnavailable;

        payment.Status = result.Status;
        payment.FailureReason = result.Reason;
        return new CreatePaymentResult(MapToResponse(payment), false);
    }

    public async Task<Result<PaymentResponse>> GetPaymentByIdAsync(Guid id)
    {
        var payment = await _repository.GetByIdAsync(id);
        if (payment is null) return Error.NotFound;
        return MapToResponse(payment);
    }

    public async Task<Result<PaymentResponse>> GetPaymentByReferenceIdAsync(string referenceId)
    {
        var payment = await _repository.GetByReferenceIdAsync(referenceId);
        if (payment is null) return Error.NotFound;
        return MapToResponse(payment);
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
