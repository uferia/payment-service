using Polly;
using Polly.CircuitBreaker;
using PaymentService.Core.Abstractions;
using PaymentService.Core.Domain;

namespace PaymentService.Infrastructure.Processors;

public class ResilientPaymentProcessor : IPaymentProcessor
{
    private readonly SimulatedPaymentProcessor _inner;
    private readonly IAsyncPolicy _policy;

    public ResilientPaymentProcessor(SimulatedPaymentProcessor inner, IAsyncPolicy policy)
    {
        _inner = inner;
        _policy = policy;
    }

    public async Task<PaymentProcessorResult> ProcessAsync(Payment payment)
    {
        try
        {
            return await _policy.ExecuteAsync(() => _inner.ProcessAsync(payment));
        }
        catch (BrokenCircuitException ex)
        {
            return PaymentProcessorResult.Failed($"Circuit breaker open: {ex.Message}");
        }
    }
}
