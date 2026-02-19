using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PaymentService.Application.Models;
using PaymentService.Application.Options;
using PaymentService.Application.Validation;

namespace PaymentService.Tests;

[TestClass]
public class ValidatorTests
{
    private CreatePaymentRequestValidator _validator = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = Options.Create(new PaymentOptions
        {
            SupportedCurrencies = ["USD", "EUR", "GBP", "PHP", "JPY", "AUD", "CAD", "CHF", "SGD", "HKD"]
        });
        _validator = new CreatePaymentRequestValidator(options);
    }

    [TestMethod]
    public void Valid_Request_Passes()
    {
        var request = new CreatePaymentRequest { Amount = 100.00m, Currency = "USD", ReferenceId = "REF-001" };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(1000001)]
    public void Invalid_Amount_Fails(double amount)
    {
        var request = new CreatePaymentRequest { Amount = (decimal)amount, Currency = "USD", ReferenceId = "REF-001" };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
    }

    [TestMethod]
    public void Amount_With_More_Than_Two_Decimals_Fails()
    {
        var request = new CreatePaymentRequest { Amount = 10.123m, Currency = "USD", ReferenceId = "REF-001" };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("US")]
    [DataRow("USDD")]
    [DataRow("XYZ")]
    [DataRow("")]
    public void Invalid_Currency_Fails(string currency)
    {
        var request = new CreatePaymentRequest { Amount = 100m, Currency = currency, ReferenceId = "REF-001" };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("usd")]
    [DataRow("EUR")]
    [DataRow("gbp")]
    public void Currency_Case_Insensitive_Passes(string currency)
    {
        var request = new CreatePaymentRequest { Amount = 100m, Currency = currency, ReferenceId = "REF-001" };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [TestMethod]
    public void Empty_ReferenceId_Fails()
    {
        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = "" };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
    }

    [TestMethod]
    public void ReferenceId_With_Special_Chars_Fails()
    {
        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = "REF@001" };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
    }

    [TestMethod]
    public void ReferenceId_Too_Long_Fails()
    {
        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = new string('a', 101) };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
    }
}
