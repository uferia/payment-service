using FluentValidation;
using FluentValidation.Results;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PaymentService.Api.Models;
using PaymentService.Api.Validation;

namespace PaymentService.Tests;

[TestClass]
public class ValidatorTests
{
    private CreatePaymentRequestValidator _validator = null!;

    [TestInitialize]
    public void Setup()
    {
        _validator = new CreatePaymentRequestValidator();
    }

    [TestMethod]
    public void Valid_Request_Passes()
    {
        var request = new CreatePaymentRequest { Amount = 100.00m, Currency = "USD", ReferenceId = "REF-001" };
        var result = _validator.Validate(request);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(1000001)]
    public void Invalid_Amount_Fails(double amount)
    {
        var request = new CreatePaymentRequest { Amount = (decimal)amount, Currency = "USD", ReferenceId = "REF-001" };
        var result = _validator.Validate(request);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void Amount_With_More_Than_Two_Decimals_Fails()
    {
        var request = new CreatePaymentRequest { Amount = 10.123m, Currency = "USD", ReferenceId = "REF-001" };
        var result = _validator.Validate(request);
        Assert.IsFalse(result.IsValid);
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
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    [DataRow("usd")]
    [DataRow("EUR")]
    [DataRow("gbp")]
    public void Currency_Case_Insensitive_Passes(string currency)
    {
        var request = new CreatePaymentRequest { Amount = 100m, Currency = currency, ReferenceId = "REF-001" };
        var result = _validator.Validate(request);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Empty_ReferenceId_Fails()
    {
        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = "" };
        var result = _validator.Validate(request);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void ReferenceId_With_Special_Chars_Fails()
    {
        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = "REF@001" };
        var result = _validator.Validate(request);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void ReferenceId_Too_Long_Fails()
    {
        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = new string('a', 101) };
        var result = _validator.Validate(request);
        Assert.IsFalse(result.IsValid);
    }
}
