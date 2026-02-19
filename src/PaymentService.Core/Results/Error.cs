namespace PaymentService.Core.Results;

public sealed record Error(string Code, string Title, string Detail, int StatusCode, int? RetryAfterSeconds = null)
{
    public string RfcType => StatusCode switch
    {
        400 => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        404 => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
        422 => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        503 => "https://tools.ietf.org/html/rfc7231#section-6.6.4",
        _   => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
    };

    public static Error PaymentRejected(string? reason) => new(
        "payment.rejected",
        "Payment Rejected",
        reason ?? "Payment was rejected.",
        422);

    public static readonly Error ServiceUnavailable = new(
        "service.unavailable",
        "Service Unavailable",
        "Payment processing is temporarily unavailable. Please retry later.",
        503,
        RetryAfterSeconds: 30);

    public static readonly Error NotFound = new(
        "resource.not_found",
        "Not Found",
        "The requested resource was not found.",
        404);
}
