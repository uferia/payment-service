using PaymentService.Core.Domain;

namespace PaymentService.Core.Abstractions;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id);
    Task<Payment?> GetByReferenceIdAsync(string referenceId);
    Task<bool> CreateAsync(Payment payment);
    Task UpdateStatusAsync(Guid id, PaymentStatus status, string? failureReason);
}
