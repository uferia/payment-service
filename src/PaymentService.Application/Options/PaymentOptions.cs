namespace PaymentService.Application.Options;

public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    public IReadOnlyList<string> SupportedCurrencies { get; init; } = [];
}
