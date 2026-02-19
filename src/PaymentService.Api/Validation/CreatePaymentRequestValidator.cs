using FluentValidation;
using PaymentService.Api.Models;

namespace PaymentService.Api.Validation;

public class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "PHP", "JPY", "AUD", "CAD", "CHF", "SGD", "HKD"
    };

    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Amount must not exceed 1,000,000.")
            .Must(HaveAtMostTwoDecimals).WithMessage("Amount must have at most 2 decimal places.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(3).WithMessage("Currency must be 3 characters.")
            .Must(c => SupportedCurrencies.Contains(c)).WithMessage("Currency is not supported.");

        RuleFor(x => x.ReferenceId)
            .NotEmpty().WithMessage("ReferenceId is required.")
            .MaximumLength(100).WithMessage("ReferenceId must not exceed 100 characters.")
            .Matches(@"^[a-zA-Z0-9\-_]+$").WithMessage("ReferenceId may only contain alphanumeric characters, hyphens, and underscores.");
    }

    private static bool HaveAtMostTwoDecimals(decimal amount)
    {
        return decimal.Round(amount, 2) == amount;
    }
}
