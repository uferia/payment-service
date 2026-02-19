using PaymentService.Application.Models;
using PaymentService.Core.Results;

namespace PaymentService.Application.Services;

public interface IPaymentService
{
    Task<Result<CreatePaymentResult>> CreatePaymentAsync(CreatePaymentRequest request);
    Task<Result<PaymentResponse>> GetPaymentByIdAsync(Guid id);
    Task<Result<PaymentResponse>> GetPaymentByReferenceIdAsync(string referenceId);
}
