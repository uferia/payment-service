using PaymentService.Api.Domain;

namespace PaymentService.Api.Processors;

public class SimulatedPaymentProcessor : IPaymentProcessor
{
    private static readonly Random _random = new();

    public async Task<PaymentProcessorResult> ProcessAsync(Payment payment)
    {
        await Task.Delay(_random.Next(50, 201));

        var amountStr = payment.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        if (amountStr.EndsWith(".77"))
            throw new HttpRequestException("Simulated network error");

        if (amountStr.EndsWith(".99"))
            return PaymentProcessorResult.Rejected("Payment rejected by processor");

        if (payment.Amount > 10000)
            return PaymentProcessorResult.InProgress();

        return PaymentProcessorResult.Success();
    }
}
