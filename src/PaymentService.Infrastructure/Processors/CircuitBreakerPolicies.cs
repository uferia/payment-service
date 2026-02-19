using Microsoft.Extensions.Configuration;
using Polly;
using Polly.CircuitBreaker;

namespace PaymentService.Infrastructure.Processors;

public static class CircuitBreakerPolicies
{
    public static IAsyncPolicy CreatePaymentProcessorPolicy(IConfiguration configuration)
    {
        int eventsBeforeBreaking = configuration.GetValue<int>("CircuitBreaker:HandledEventsAllowedBeforeBreaking", 3);
        int durationOfBreakSeconds = configuration.GetValue<int>("CircuitBreaker:DurationOfBreakSeconds", 30);

        var circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(eventsBeforeBreaking, TimeSpan.FromSeconds(durationOfBreakSeconds));

        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }
}
