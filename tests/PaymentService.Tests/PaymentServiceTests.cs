using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PaymentService.Application.Models;
using PaymentService.Core.Abstractions;
using PaymentService.Core.Domain;

namespace PaymentService.Tests;

[TestClass]
public class PaymentServiceTests
{
    private Mock<IPaymentRepository> _repositoryMock = null!;
    private Mock<IPaymentProcessor> _processorMock = null!;
    private PaymentService.Application.Services.PaymentService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _repositoryMock = new Mock<IPaymentRepository>();
        _processorMock = new Mock<IPaymentProcessor>();
        _service = new PaymentService.Application.Services.PaymentService(_repositoryMock.Object, _processorMock.Object);
    }

    [TestMethod]
    public async Task CreatePayment_Valid_ReturnsCompleted()
    {
        _repositoryMock.Setup(r => r.GetByReferenceIdAsync("REF-001")).ReturnsAsync((Payment?)null);
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Payment>())).ReturnsAsync(true);
        _processorMock.Setup(p => p.ProcessAsync(It.IsAny<Payment>())).ReturnsAsync(PaymentProcessorResult.Success());
        _repositoryMock.Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<PaymentStatus>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = "REF-001" };
        var result = await _service.CreatePaymentAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsExisting.Should().BeFalse();
        result.Value.Payment.Status.Should().Be("Completed");
    }

    [TestMethod]
    public async Task CreatePayment_Duplicate_ReturnsExisting()
    {
        var existing = new Payment { Id = Guid.NewGuid(), ReferenceId = "REF-001", Amount = 100m, Currency = "USD", Status = PaymentStatus.Completed, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        _repositoryMock.Setup(r => r.GetByReferenceIdAsync("REF-001")).ReturnsAsync(existing);

        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = "REF-001" };
        var result = await _service.CreatePaymentAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsExisting.Should().BeTrue();
        result.Value.Payment.Status.Should().Be("Completed");
    }

    [TestMethod]
    public async Task CreatePayment_ConcurrentDuplicate_ReReadsAndReturnsExisting()
    {
        var existing = new Payment { Id = Guid.NewGuid(), ReferenceId = "REF-002", Amount = 50m, Currency = "EUR", Status = PaymentStatus.Pending, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        _repositoryMock.SetupSequence(r => r.GetByReferenceIdAsync("REF-002"))
            .ReturnsAsync((Payment?)null)
            .ReturnsAsync(existing);
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Payment>())).ReturnsAsync(false);

        var request = new CreatePaymentRequest { Amount = 50m, Currency = "EUR", ReferenceId = "REF-002" };
        var result = await _service.CreatePaymentAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsExisting.Should().BeTrue();
    }

    [TestMethod]
    public async Task CreatePayment_Rejected_ReturnsRejectedError()
    {
        _repositoryMock.Setup(r => r.GetByReferenceIdAsync(It.IsAny<string>())).ReturnsAsync((Payment?)null);
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Payment>())).ReturnsAsync(true);
        _processorMock.Setup(p => p.ProcessAsync(It.IsAny<Payment>())).ReturnsAsync(PaymentProcessorResult.Rejected("Rejected by bank"));
        _repositoryMock.Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<PaymentStatus>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = "REF-003" };
        var result = await _service.CreatePaymentAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("payment.rejected");
        result.Error.StatusCode.Should().Be(422);
    }

    [TestMethod]
    public async Task CreatePayment_LargeAmount_ReturnsProcessing()
    {
        _repositoryMock.Setup(r => r.GetByReferenceIdAsync(It.IsAny<string>())).ReturnsAsync((Payment?)null);
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Payment>())).ReturnsAsync(true);
        _processorMock.Setup(p => p.ProcessAsync(It.IsAny<Payment>())).ReturnsAsync(PaymentProcessorResult.InProgress());
        _repositoryMock.Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<PaymentStatus>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest { Amount = 20000m, Currency = "USD", ReferenceId = "REF-004" };
        var result = await _service.CreatePaymentAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsExisting.Should().BeFalse();
        result.Value.Payment.Status.Should().Be("Processing");
    }

    [TestMethod]
    public async Task CreatePayment_Failed_ReturnsServiceUnavailableError()
    {
        _repositoryMock.Setup(r => r.GetByReferenceIdAsync(It.IsAny<string>())).ReturnsAsync((Payment?)null);
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Payment>())).ReturnsAsync(true);
        _processorMock.Setup(p => p.ProcessAsync(It.IsAny<Payment>())).ReturnsAsync(PaymentProcessorResult.Failed("Circuit breaker open"));
        _repositoryMock.Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<PaymentStatus>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = "REF-005" };
        var result = await _service.CreatePaymentAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("service.unavailable");
        result.Error.StatusCode.Should().Be(503);
    }
}
