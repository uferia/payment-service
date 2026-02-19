namespace PaymentService.Application.Models;

public sealed record CreatePaymentResult(PaymentResponse Payment, bool IsExisting);
