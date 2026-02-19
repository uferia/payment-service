namespace PaymentService.Api.Domain;

public enum PaymentStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Rejected = 3,
    Failed = 4
}
