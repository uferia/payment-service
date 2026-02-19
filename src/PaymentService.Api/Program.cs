using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using FluentValidation;
using Polly;
using Scalar.AspNetCore;
using Serilog;
using System.Threading.RateLimiting;
using PaymentService.Api.Auth;
using PaymentService.Api.Data;
using PaymentService.Api.Middleware;
using PaymentService.Api.Models;
using PaymentService.Api.Processors;
using PaymentService.Api.Repositories;
using PaymentService.Api.Validation;

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
    builder.Services.AddScoped<PaymentService.Api.Services.IPaymentService, PaymentService.Api.Services.PaymentService>();

    // Validation
    builder.Services.AddScoped<IValidator<CreatePaymentRequest>, CreatePaymentRequestValidator>();

    // Auth
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddAuthorization();

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

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    var app = builder.Build();

    // Initialize DB
    var dbInitializer = app.Services.GetRequiredService<DatabaseInitializer>();
    dbInitializer.Initialize();

    // Middleware
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseHttpsRedirection();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options.Theme = ScalarTheme.BluePlanet);
    }

    app.MapControllers();

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
