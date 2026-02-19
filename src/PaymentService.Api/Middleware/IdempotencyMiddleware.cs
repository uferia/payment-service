using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PaymentService.Application.Models;
using PaymentService.Api.Serialization;

namespace PaymentService.Api.Middleware;

public sealed class IdempotencyMiddleware
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string IdempotencyReplayHeader = "Idempotency-Replay";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;

    public IdempotencyMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        _next = next;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Post ||
            !context.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var rawKey))
        {
            await _next(context);
            return;
        }

        if (!Guid.TryParse(rawKey, out _))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Invalid Idempotency Key",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The Idempotency-Key header must be a valid GUID.",
                TraceId = context.TraceIdentifier
            }, PaymentJsonOptions.Default));
            return;
        }

        var userId = context.User.Identity?.Name ?? "anonymous";
        var cacheKey = $"idempotency:{userId}:{rawKey}";

        if (_cache.TryGetValue(cacheKey, out IdempotencyCacheEntry? cached))
        {
            context.Response.StatusCode = cached!.StatusCode;
            context.Response.ContentType = cached.ContentType;
            context.Response.Headers.Append(IdempotencyReplayHeader, "true");
            if (cached.Location is not null)
                context.Response.Headers.Location = cached.Location;
            await context.Response.WriteAsync(cached.Body);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            buffer.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(buffer, leaveOpen: true);
            var responseBody = await reader.ReadToEndAsync();

            if (context.Response.StatusCode is >= 200 and < 300)
            {
                _cache.Set(cacheKey, new IdempotencyCacheEntry
                {
                    StatusCode = context.Response.StatusCode,
                    ContentType = context.Response.ContentType ?? "application/json",
                    Body = responseBody,
                    Location = context.Response.Headers.Location.FirstOrDefault()
                }, CacheDuration);
            }
        }
        finally
        {
            buffer.Seek(0, SeekOrigin.Begin);
            await buffer.CopyToAsync(originalBody);
            context.Response.Body = originalBody;
        }
    }

    private sealed record IdempotencyCacheEntry
    {
        public int StatusCode { get; init; }
        public string ContentType { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string? Location { get; init; }
    }
}
