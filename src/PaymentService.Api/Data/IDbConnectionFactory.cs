using System.Data;

namespace PaymentService.Api.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
