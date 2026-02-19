using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using PaymentService.Application.Models;
using PaymentService.Api.Serialization;
using System.Text.Json;

namespace PaymentService.Api.Handlers;

public sealed class ValidationExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ValidationExceptionHandler> _logger;

    public ValidationExceptionHandler(ILogger<ValidationExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
            return false;

        _logger.LogWarning(validationException, "Validation error");

        httpContext.Response.StatusCode = 400;
        httpContext.Response.ContentType = "application/json";

        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var response = new ErrorResponse
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Validation Failed",
            Status = 400,
            Detail = "One or more validation errors occurred.",
            TraceId = httpContext.TraceIdentifier,
            Errors = errors
        };

        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(response, PaymentJsonOptions.Default), cancellationToken);
        return true;
    }
}
