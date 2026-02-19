namespace PaymentService.Application.Models;

public class CreatePaymentRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
}
