using Microsoft.AspNetCore.Diagnostics;
using PaymentService.Application.Models;
using PaymentService.Api.Serialization;
using System.Text.Json;

namespace PaymentService.Api.Handlers;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception");

        httpContext.Response.StatusCode = 500;
        httpContext.Response.ContentType = "application/json";

        var response = new ErrorResponse
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Title = "Internal Server Error",
            Status = 500,
            Detail = "An unexpected error occurred.",
            TraceId = httpContext.TraceIdentifier
        };

        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(response, PaymentJsonOptions.Default), cancellationToken);
        return true;
    }
}
