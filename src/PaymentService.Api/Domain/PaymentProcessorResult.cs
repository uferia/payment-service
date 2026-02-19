namespace PaymentService.Api.Domain;

public class PaymentProcessorResult
{
    public PaymentStatus Status { get; private set; }
    public string? Reason { get; private set; }

    private PaymentProcessorResult(PaymentStatus status, string? reason = null)
    {
        Status = status;
        Reason = reason;
    }

    public static PaymentProcessorResult Success() => new(PaymentStatus.Completed);
    public static PaymentProcessorResult InProgress() => new(PaymentStatus.Processing);
    public static PaymentProcessorResult Rejected(string reason) => new(PaymentStatus.Rejected, reason);
    public static PaymentProcessorResult Failed(string reason) => new(PaymentStatus.Failed, reason);
}
