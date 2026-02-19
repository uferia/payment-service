using FluentValidation;
using Microsoft.Extensions.Options;
using PaymentService.Application.Models;
using PaymentService.Application.Options;

namespace PaymentService.Application.Validation;

public class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator(IOptions<PaymentOptions> paymentOptions)
    {
        var supportedCurrencies = new HashSet<string>(
            paymentOptions.Value.SupportedCurrencies,
            StringComparer.OrdinalIgnoreCase);

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Amount must not exceed 1,000,000.")
            .Must(HaveAtMostTwoDecimals).WithMessage("Amount must have at most 2 decimal places.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(3).WithMessage("Currency must be 3 characters.")
            .Must(c => supportedCurrencies.Contains(c)).WithMessage("Currency is not supported.");

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
