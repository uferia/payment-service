using PaymentService.Api.Domain;

namespace PaymentService.Api.Processors;

public interface IPaymentProcessor
{
    Task<PaymentProcessorResult> ProcessAsync(Payment payment);
}
