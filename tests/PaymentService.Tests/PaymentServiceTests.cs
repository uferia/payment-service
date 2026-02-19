using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PaymentService.Api.Domain;
using PaymentService.Api.Models;
using PaymentService.Api.Processors;
using PaymentService.Api.Repositories;

namespace PaymentService.Tests;

[TestClass]
public class PaymentServiceTests
{
    private Mock<IPaymentRepository> _repositoryMock = null!;
    private Mock<IPaymentProcessor> _processorMock = null!;
    private PaymentService.Api.Services.PaymentService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _repositoryMock = new Mock<IPaymentRepository>();
        _processorMock = new Mock<IPaymentProcessor>();
        _service = new PaymentService.Api.Services.PaymentService(_repositoryMock.Object, _processorMock.Object);
    }

    [TestMethod]
    public async Task CreatePayment_Valid_ReturnsCompleted()
    {
        _repositoryMock.Setup(r => r.GetByReferenceIdAsync("REF-001")).ReturnsAsync((Payment?)null);
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Payment>())).ReturnsAsync(true);
        _processorMock.Setup(p => p.ProcessAsync(It.IsAny<Payment>())).ReturnsAsync(PaymentProcessorResult.Success());
        _repositoryMock.Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<PaymentStatus>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = "REF-001" };
        var (response, isExisting) = await _service.CreatePaymentAsync(request);

        Assert.IsFalse(isExisting);
        Assert.AreEqual("Completed", response.Status);
    }

    [TestMethod]
    public async Task CreatePayment_Duplicate_ReturnsExisting()
    {
        var existing = new Payment { Id = Guid.NewGuid(), ReferenceId = "REF-001", Amount = 100m, Currency = "USD", Status = PaymentStatus.Completed, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        _repositoryMock.Setup(r => r.GetByReferenceIdAsync("REF-001")).ReturnsAsync(existing);

        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = "REF-001" };
        var (response, isExisting) = await _service.CreatePaymentAsync(request);

        Assert.IsTrue(isExisting);
        Assert.AreEqual("Completed", response.Status);
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
        var (response, isExisting) = await _service.CreatePaymentAsync(request);

        Assert.IsTrue(isExisting);
    }

    [TestMethod]
    public async Task CreatePayment_Rejected_ReturnsRejected()
    {
        _repositoryMock.Setup(r => r.GetByReferenceIdAsync(It.IsAny<string>())).ReturnsAsync((Payment?)null);
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Payment>())).ReturnsAsync(true);
        _processorMock.Setup(p => p.ProcessAsync(It.IsAny<Payment>())).ReturnsAsync(PaymentProcessorResult.Rejected("Rejected by bank"));
        _repositoryMock.Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<PaymentStatus>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = "REF-003" };
        var (response, isExisting) = await _service.CreatePaymentAsync(request);

        Assert.IsFalse(isExisting);
        Assert.AreEqual("Rejected", response.Status);
    }

    [TestMethod]
    public async Task CreatePayment_LargeAmount_ReturnsProcessing()
    {
        _repositoryMock.Setup(r => r.GetByReferenceIdAsync(It.IsAny<string>())).ReturnsAsync((Payment?)null);
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Payment>())).ReturnsAsync(true);
        _processorMock.Setup(p => p.ProcessAsync(It.IsAny<Payment>())).ReturnsAsync(PaymentProcessorResult.InProgress());
        _repositoryMock.Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<PaymentStatus>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest { Amount = 20000m, Currency = "USD", ReferenceId = "REF-004" };
        var (response, isExisting) = await _service.CreatePaymentAsync(request);

        Assert.IsFalse(isExisting);
        Assert.AreEqual("Processing", response.Status);
    }

    [TestMethod]
    public async Task CreatePayment_Failed_ReturnsFailed()
    {
        _repositoryMock.Setup(r => r.GetByReferenceIdAsync(It.IsAny<string>())).ReturnsAsync((Payment?)null);
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Payment>())).ReturnsAsync(true);
        _processorMock.Setup(p => p.ProcessAsync(It.IsAny<Payment>())).ReturnsAsync(PaymentProcessorResult.Failed("Circuit breaker open"));
        _repositoryMock.Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<PaymentStatus>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

        var request = new CreatePaymentRequest { Amount = 100m, Currency = "USD", ReferenceId = "REF-005" };
        var (response, isExisting) = await _service.CreatePaymentAsync(request);

        Assert.IsFalse(isExisting);
        Assert.AreEqual("Failed", response.Status);
    }
}
