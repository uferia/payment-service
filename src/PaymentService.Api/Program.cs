using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using FluentValidation;
using Polly;
using Scalar.AspNetCore;
using Serilog;
using System.Threading.RateLimiting;
using AppPaymentService = PaymentService.Application.Services.PaymentService;
using PaymentService.Api.Auth;
using PaymentService.Api.Handlers;
using PaymentService.Api.Middleware;
using PaymentService.Application.Models;
using PaymentService.Application.Options;
using PaymentService.Application.Services;
using PaymentService.Application.Validation;
using PaymentService.Core.Abstractions;
using PaymentService.Infrastructure.Data;
using PaymentService.Infrastructure.Processors;
using PaymentService.Infrastructure.Repositories;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration)
                     .ReadFrom.Services(services));

    // Azure Key Vault
    var vaultUri = builder.Configuration["AzureKeyVault:VaultUri"];
    if (!string.IsNullOrEmpty(vaultUri) && !vaultUri.Contains("<your-keyvault-name>"))
    {
        builder.Configuration.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential());
    }

    // Database
    builder.Services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
    builder.Services.AddSingleton<DatabaseInitializer>();

    // Repository
    builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

    // Circuit breaker policy
    var policy = CircuitBreakerPolicies.CreatePaymentProcessorPolicy(builder.Configuration);
    builder.Services.AddSingleton<IAsyncPolicy>(policy);
    builder.Services.AddSingleton<SimulatedPaymentProcessor>();
    builder.Services.AddScoped<IPaymentProcessor>(sp =>
        new ResilientPaymentProcessor(
            sp.GetRequiredService<SimulatedPaymentProcessor>(),
            sp.GetRequiredService<IAsyncPolicy>()));

    // Service
    builder.Services.AddScoped<IPaymentService, AppPaymentService>();

    // Validation
    builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.SectionName));
    builder.Services.AddScoped<IValidator<CreatePaymentRequest>, CreatePaymentRequestValidator>();

    // Auth
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddAuthorization();

    // Startup validation — fail fast on missing required config
    var jwtKey = builder.Configuration["Jwt:SecretKey"];
    if (string.IsNullOrWhiteSpace(jwtKey))
        throw new InvalidOperationException("Jwt:SecretKey is required but not configured.");

    // Rate limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1)
                }));
    });

    // Exception handlers
    builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();
    builder.Services.AddMemoryCache();

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Initialize DB
    var dbInitializer = app.Services.GetRequiredService<DatabaseInitializer>();
    dbInitializer.Initialize();

    // Middleware
    app.UseExceptionHandler();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseHttpsRedirection();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<IdempotencyMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options.Theme = ScalarTheme.BluePlanet);
    }

    app.MapControllers();
    app.MapHealthChecks("/healthz");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
