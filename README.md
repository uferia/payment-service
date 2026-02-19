# Payment Service

A production-grade .NET 9 REST API for processing payments. Features JWT authentication, two-layer idempotency, SQLite persistence via Dapper, Polly circuit-breaker resiliency, rate limiting, and structured Serilog logging — organised as a clean-architecture class-library solution.

## Architecture

The solution is split into four projects with a strict inward dependency rule — outer layers depend on inner ones, never the reverse.

```
PaymentService/
├── src/
│   ├── PaymentService.Core/              # No dependencies — pure domain
│   │   ├── Abstractions/                 #   IPaymentRepository, IPaymentProcessor
│   │   ├── Domain/                       #   Payment, PaymentStatus, PaymentProcessorResult
│   │   └── Results/                      #   Result<T>, Error
│   │
│   ├── PaymentService.Application/       # Depends on: Core
│   │   ├── Models/                       #   Request/response DTOs, ErrorResponse
│   │   ├── Options/                      #   PaymentOptions (IOptions<T>)
│   │   ├── Services/                     #   IPaymentService, PaymentService
│   │   └── Validation/                   #   CreatePaymentRequestValidator (FluentValidation)
│   │
│   ├── PaymentService.Infrastructure/    # Depends on: Core
│   │   ├── Data/                         #   IDbConnectionFactory, SqliteConnectionFactory, DatabaseInitializer
│   │   ├── Processors/                   #   SimulatedPaymentProcessor, ResilientPaymentProcessor, CircuitBreakerPolicies
│   │   └── Repositories/                 #   PaymentRepository (Dapper + SQLite)
│   │
│   └── PaymentService.Api/               # Depends on: Core + Application + Infrastructure
│       ├── Auth/                         #   ITokenService, TokenService, JwtConfiguration
│       ├── Controllers/                  #   PaymentsController, AuthController
│       ├── Handlers/                     #   ValidationExceptionHandler, GlobalExceptionHandler
│       ├── Middleware/                   #   IdempotencyMiddleware, RequestLoggingMiddleware
│       ├── Serialization/                #   Shared JsonSerializerOptions (camelCase)
│       └── Program.cs                    #   Composition root
│
└── tests/
    └── PaymentService.Tests/
        ├── ValidatorTests.cs             # Unit — input validation rules
        ├── PaymentServiceTests.cs        # Unit — business logic (Moq)
        └── IntegrationTests.cs          # End-to-end HTTP (WebApplicationFactory)
```

### Dependency rules

| Layer | May reference |
|---|---|
| `Core` | Nothing (pure .NET types only) |
| `Application` | `Core` |
| `Infrastructure` | `Core` |
| `Api` | `Core`, `Application`, `Infrastructure` |

---

## Tech Stack

| Concern | Technology |
|---|---|
| Framework | ASP.NET Core 9 / .NET 9 |
| Persistence | SQLite via Dapper |
| Authentication | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) |
| Validation | FluentValidation 11 |
| Resiliency | Polly 7 — retry + circuit breaker |
| Logging | Serilog — structured, console sink |
| API Docs | Scalar + OpenAPI *(development only)* |
| Secrets | Azure Key Vault via `DefaultAzureCredential` *(optional)* |
| Rate Limiting | ASP.NET Core built-in `RateLimiter` |
| Testing | MSTest + Moq + FluentAssertions + `WebApplicationFactory` |
| CI | GitHub Actions |

---

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Run locally

```bash
cd src/PaymentService.Api
dotnet run
```

The API starts on `https://localhost:7236` / `http://localhost:5144`.

> The development JWT secret is in `appsettings.Development.json`. It is intentionally committed — it is non-sensitive and scoped to the Development environment only. The `/api/auth/token` endpoint returns `404 Not Found` in any other environment.

### Production configuration

The application throws `InvalidOperationException` at startup if `Jwt:SecretKey` is absent. Supply required values via environment variables or Azure Key Vault:

| Key | Description |
|---|---|
| `Jwt__SecretKey` | HS256 signing key — minimum 32 characters |
| `ConnectionStrings__DefaultConnection` | SQLite connection string |
| `AzureKeyVault__VaultUri` | *(optional)* Azure Key Vault URI |

### Testing locally with Scalar

1. Run the app (`dotnet run`)
2. Open `https://localhost:7236/scalar/v1`
3. Call `POST /api/auth/token` — no auth needed — copy the `token` value
4. Click **Authentication → Bearer Token** in the Scalar UI and paste the token
5. All subsequent requests will carry `Authorization: Bearer <token>` automatically

> Tokens expire after **1 hour**. Repeat steps 3–4 to refresh.

### Health check

```
GET /healthz   →  200 OK
```

### Azure Key Vault (optional)

Set `AzureKeyVault:VaultUri` in `appsettings.json`. The app uses `DefaultAzureCredential` and loads secrets (including `Jwt:SecretKey`) from Key Vault at startup.

---

## API Reference

### Authentication

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/token` | None | Returns a development JWT. **Returns `404` outside Development.** |

### Payments

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/payments` | Bearer | Create a payment |
| `GET` | `/api/payments/{id}` | Bearer | Get payment by ID |
| `GET` | `/api/payments/reference/{referenceId}` | Bearer | Get payment by reference ID |

### Payments

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/payments` | Bearer | Create a payment — supports `Idempotency-Key` header |
| `GET` | `/api/payments/{id}` | Bearer | Get payment by ID |
| `GET` | `/api/payments/reference/{referenceId}` | Bearer | Get payment by reference ID |

---

## Creating a Payment

```bash
curl -X POST https://localhost:7236/api/payments \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"amount": 99.50, "currency": "USD", "referenceId": "order-12345"}'
```

### Validation rules

| Field | Rules |
|---|---|
| `amount` | > 0, ≤ 1,000,000, max 2 decimal places |
| `currency` | Exactly 3 characters, must be in the supported list (case-insensitive) |
| `referenceId` | 1–100 characters, alphanumeric + hyphens/underscores only |

### Supported currencies

Configured in `appsettings.json` — no code change required:

```json
"Payment": {
  "SupportedCurrencies": [ "USD", "EUR", "GBP", "PHP", "JPY", "AUD", "CAD", "CHF", "SGD", "HKD" ]
}
```

Override in production via environment variable:

```
Payment__SupportedCurrencies__0=USD
Payment__SupportedCurrencies__1=EUR
```

---

## Idempotency

Two independent layers prevent duplicate payments.

### Layer 1 — Business key (`referenceId`)

`referenceId` is the natural business key. A `UNIQUE INDEX` on the `Payments` table enforces uniqueness at the database level. The service checks for an existing record before inserting and re-reads on concurrent-insert race conditions.

| Scenario | HTTP status |
|---|---|
| First request for a `referenceId` | `201 Created` |
| Repeat request with same `referenceId` | `200 OK` — existing payment returned |

### Layer 2 — HTTP `Idempotency-Key` header

Send an `Idempotency-Key: <uuid>` header on `POST /api/payments`. The server caches the exact response (status code + body + `Location` header) for 24 hours per authenticated user.

```bash
curl -X POST https://localhost:7236/api/payments \
  -H "Authorization: Bearer <token>" \
  -H "Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000" \
  -H "Content-Type: application/json" \
  -d '{"amount": 99.50, "currency": "USD", "referenceId": "order-12345"}'
```

| Condition | Behaviour |
|---|---|
| Key not seen before | Process normally, cache response for 24 h |
| Key seen (cached) | Return cached response + `Idempotency-Replay: true` header |
| Key is not a valid GUID | `400 Bad Request` |
| Non-2xx response | Not cached — client should retry |

> Cache is scoped per authenticated user — one user cannot replay another user's responses.

---

## Error Responses

All errors follow [RFC 7807](https://tools.ietf.org/html/rfc7807) Problem Details shape:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation Failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "traceId": "0HN5K2R4Q1ABC:00000001",
  "errors": {
    "Amount": ["Amount must be greater than 0."]
  }
}
```

| HTTP Status | Meaning |
|---|---|
| `400` | Validation failure or invalid idempotency key |
| `401` | Missing or invalid JWT |
| `404` | Payment not found |
| `422` | Payment rejected by processor |
| `429` | Rate limit exceeded |
| `503` | Circuit breaker open — retry after `retryAfterSeconds` |
| `500` | Unexpected server error |

---

## Payment Simulation

`SimulatedPaymentProcessor` simulates real-world processor behaviour:

| Amount ends in | Result |
|---|---|
| `.77` | Throws `HttpRequestException` → triggers retry/circuit-breaker |
| `.99` | `422 Rejected` |
| `> 10,000` | `Processing` (async/manual review) |
| anything else | `201 Completed` |

---

## Circuit Breaker & Retry

Configured via `appsettings.json`:

```json
"CircuitBreaker": {
  "HandledEventsAllowedBeforeBreaking": 3,
  "DurationOfBreakSeconds": 30
}
```

- **Retry policy**: 2 retries with exponential back-off (2 s, 4 s)
- **Circuit breaker**: opens after 3 consecutive `HttpRequestException`s, stays open for 30 s
- When the circuit is open, payments return `HTTP 503` with `retryAfterSeconds: 30`

---

## Rate Limiting

100 requests per minute per IP address (fixed window). Returns `HTTP 429 Too Many Requests` when exceeded.

---

## Running Tests

```bash
dotnet test
```

| Suite | What is tested |
|---|---|
| `ValidatorTests` | Amount, currency, and `referenceId` validation rules |
| `PaymentServiceTests` | Business logic — create, duplicate, race condition, rejected, failed, processing |
| `IntegrationTests` | Full HTTP stack — 201 created, 200 idempotent, 400 validation, 401 auth, idempotency-key caching |

---

## CI

GitHub Actions runs on every push and pull request to `main`:

1. Restore dependencies
2. Build in Release configuration
3. Run all tests (JWT key injected via environment variable)

See [`.github/workflows/ci.yml`](.github/workflows/ci.yml).