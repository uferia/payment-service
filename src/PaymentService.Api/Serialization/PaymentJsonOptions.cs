using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaymentService.Api.Serialization;

internal static class PaymentJsonOptions
{
    internal static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
