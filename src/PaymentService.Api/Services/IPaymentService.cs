using PaymentService.Api.Models;

namespace PaymentService.Api.Services;

public interface IPaymentService
{
    Task<(PaymentResponse Response, bool IsExisting)> CreatePaymentAsync(CreatePaymentRequest request);
    Task<PaymentResponse?> GetPaymentByIdAsync(Guid id);
    Task<PaymentResponse?> GetPaymentByReferenceIdAsync(string referenceId);
}
