namespace PaymentService.Application.Models;

public class PaymentResponse
{
    public Guid Id { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
