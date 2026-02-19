using PaymentService.Core.Domain;

namespace PaymentService.Core.Abstractions;

public interface IPaymentProcessor
{
    Task<PaymentProcessorResult> ProcessAsync(Payment payment);
}
