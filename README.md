# Payment Service

A production-ready .NET 9 REST API for processing payments, featuring JWT authentication, idempotent payment creation, SQLite persistence, circuit-breaker resiliency, and rate limiting.

## Architecture

```
PaymentService/
├── src/
│   └── PaymentService.Api/
│       ├── Auth/               # JWT authentication configuration
│       ├── Controllers/        # HTTP endpoints (Payments, Auth)
│       ├── Data/               # Database connection factory & initializer
│       ├── Domain/             # Core domain models (Payment, PaymentStatus, etc.)
│       ├── Middleware/         # Exception handling & request logging
│       ├── Models/             # Request/response DTOs
│       ├── Processors/         # Payment processor with Polly circuit breaker
│       ├── Repositories/       # Data access layer (Dapper + SQLite)
│       ├── Services/           # Business logic layer
│       └── Validation/         # FluentValidation request validators
└── tests/
    └── PaymentService.Tests/
        ├── ValidatorTests.cs       # Unit tests for input validation
        ├── PaymentServiceTests.cs  # Unit tests for business logic
        └── IntegrationTests.cs     # End-to-end HTTP tests
```

## Tech Stack

| Concern | Technology |
|---|---|
| Framework | ASP.NET Core 9 |
| Persistence | SQLite via Dapper |
| Authentication | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) |
| Validation | FluentValidation |
| Resiliency | Polly (retry + circuit breaker) |
| Logging | Serilog (structured, console sink) |
| API Docs | Scalar + OpenAPI (dev only) |
| Secrets | Azure Key Vault (optional) |
| Rate Limiting | ASP.NET Core built-in (`RateLimiter`) |
| Testing | MSTest + Moq + `WebApplicationFactory` |

## Setup

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Run locally

```bash
cd src/PaymentService.Api
dotnet run
```

The API starts on `https://localhost:5001` / `http://localhost:5000`.

> **Development JWT secret** is configured in `appsettings.Development.json`. Never use in production.

### Azure Key Vault (optional)

Set `AzureKeyVault:VaultUri` in `appsettings.json` to your vault URI. The app uses `DefaultAzureCredential` and will automatically load secrets (including `Jwt:SecretKey`) from Key Vault at startup.

## API Endpoints

### Authentication

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/token` | None | Get a development JWT token |

### Payments

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/payments` | Bearer | Create a payment (idempotent) |
| `GET` | `/api/payments/{id}` | Bearer | Get payment by ID |
| `GET` | `/api/payments/reference/{referenceId}` | Bearer | Get payment by reference ID |

### API Documentation

Interactive Scalar UI is available at `/scalar/v1` in Development mode.

## Authentication

All `/api/payments` endpoints require a `Bearer` JWT token.

**Get a token (development only):**
```bash
curl -X POST http://localhost:5000/api/auth/token
```

**Use the token:**
```bash
curl -H "Authorization: Bearer <token>" http://localhost:5000/api/payments/<id>
```

## Creating a Payment

```bash
curl -X POST http://localhost:5000/api/payments \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"amount": 99.50, "currency": "USD", "referenceId": "order-12345"}'
```

**Supported currencies:** USD, EUR, GBP, PHP, JPY, AUD, CAD, CHF, SGD, HKD

**Validation rules:**
- `amount`: greater than 0, at most 1,000,000, max 2 decimal places
- `currency`: exactly 3 characters, must be a supported currency (case-insensitive)
- `referenceId`: 1–100 characters, alphanumeric + hyphens/underscores only

### Idempotency

Submitting the same `referenceId` twice returns the existing payment with HTTP `200 OK` instead of creating a duplicate.

## Payment Simulation Rules

The `SimulatedPaymentProcessor` simulates real-world processor behavior:

| Amount ends in | Result |
|---|---|
| `.77` | Throws `HttpRequestException` (triggers retry/circuit-breaker) |
| `.99` | `Rejected` |
| `> 10000` | `Processing` (async/manual review) |
| anything else | `Completed` |

## Circuit Breaker

Configured via `appsettings.json`:

```json
"CircuitBreaker": {
  "HandledEventsAllowedBeforeBreaking": 3,
  "DurationOfBreakSeconds": 30
}
```

When the circuit is open, payments return `HTTP 503` with `RetryAfterSeconds: 30`.

A retry policy (2 retries with exponential backoff) wraps the circuit breaker.

## Rate Limiting

100 requests per minute per IP address (fixed window). Returns `HTTP 429` when exceeded.

## Running Tests

```bash
dotnet test
```

| Test suite | Coverage |
|---|---|
| `ValidatorTests` | Input validation (amounts, currencies, reference IDs) |
| `PaymentServiceTests` | Business logic (create, duplicate, rejected, failed, processing) |
| `IntegrationTests` | Full HTTP stack (201 created, 200 idempotent, 400 validation, 401 auth) |
