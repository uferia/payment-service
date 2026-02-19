using Dapper;
using Microsoft.Data.Sqlite;
using PaymentService.Api.Data;
using PaymentService.Api.Domain;

namespace PaymentService.Api.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PaymentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private class PaymentRow
    {
        public string Id { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public double Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public int Status { get; set; }
        public string? FailureReason { get; set; }
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string UpdatedAtUtc { get; set; } = string.Empty;

        public Payment ToPayment() => new()
        {
            Id = Guid.Parse(Id),
            ReferenceId = ReferenceId,
            Amount = (decimal)Amount,
            Currency = Currency,
            Status = (PaymentStatus)Status,
            FailureReason = FailureReason,
            CreatedAtUtc = DateTime.Parse(CreatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
            UpdatedAtUtc = DateTime.Parse(UpdatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind)
        };
    }

    public async Task<Payment?> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<PaymentRow>(
            "SELECT * FROM Payments WHERE Id = @Id",
            new { Id = id.ToString() });
        return row?.ToPayment();
    }

    public async Task<Payment?> GetByReferenceIdAsync(string referenceId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<PaymentRow>(
            "SELECT * FROM Payments WHERE ReferenceId = @ReferenceId",
            new { ReferenceId = referenceId });
        return row?.ToPayment();
    }

    public async Task<bool> CreateAsync(Payment payment)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            await connection.ExecuteAsync(
                @"INSERT INTO Payments (Id, ReferenceId, Amount, Currency, Status, FailureReason, CreatedAtUtc, UpdatedAtUtc)
                  VALUES (@Id, @ReferenceId, @Amount, @Currency, @Status, @FailureReason, @CreatedAtUtc, @UpdatedAtUtc)",
                new
                {
                    Id = payment.Id.ToString(),
                    payment.ReferenceId,
                    Amount = (double)payment.Amount,
                    payment.Currency,
                    Status = (int)payment.Status,
                    payment.FailureReason,
                    CreatedAtUtc = payment.CreatedAtUtc.ToString("O"),
                    UpdatedAtUtc = payment.UpdatedAtUtc.ToString("O")
                });
            return true;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return false;
        }
    }

    public async Task UpdateStatusAsync(Guid id, PaymentStatus status, string? failureReason)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE Payments SET Status = @Status, FailureReason = @FailureReason, UpdatedAtUtc = @UpdatedAtUtc
              WHERE Id = @Id",
            new
            {
                Id = id.ToString(),
                Status = (int)status,
                FailureReason = failureReason,
                UpdatedAtUtc = DateTime.UtcNow.ToString("O")
            });
    }
}
