using Dapper;

namespace PaymentService.Infrastructure.Data;

public class DatabaseInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DatabaseInitializer(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {
        using var connection = _connectionFactory.CreateConnection();

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Payments (
                Id TEXT PRIMARY KEY,
                ReferenceId TEXT NOT NULL,
                Amount REAL NOT NULL,
                Currency TEXT NOT NULL,
                Status INTEGER NOT NULL,
                FailureReason TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS IX_Payments_ReferenceId ON Payments (ReferenceId);
        ");
    }
}
