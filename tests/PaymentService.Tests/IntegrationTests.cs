using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PaymentService.Application.Models;

namespace PaymentService.Tests;

[TestClass]
public class IntegrationTests
{
    private static WebApplicationFactory<Program> _factory = null!;
    private static HttpClient _client = null!;
    private static string _tempDbPath = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"payments_test_{Guid.NewGuid()}.db");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_tempDbPath}");
                builder.UseSetting("Jwt:SecretKey", "TestSecretKeyThatIsAtLeast32CharactersLong!!");
                builder.UseSetting("AzureKeyVault:VaultUri", "");
            });

        _client = _factory.CreateClient();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _client?.Dispose();
        _factory?.Dispose();
        if (File.Exists(_tempDbPath))
            File.Delete(_tempDbPath);
    }

    private static async Task<string> GetTokenAsync()
    {
        var response = await _client.PostAsync("/api/auth/token", null);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(content);
        return json.GetProperty("token").GetString()!;
    }

    [TestMethod]
    public async Task Post_Payment_Returns201()
    {
        var token = await GetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePaymentRequest { Amount = 50m, Currency = "USD", ReferenceId = $"REF-{Guid.NewGuid()}" };
        var response = await _client.PostAsJsonAsync("/api/payments", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [TestMethod]
    public async Task Post_Duplicate_Payment_Returns200_With_Same_Body()
    {
        var token = await GetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var referenceId = $"REF-DUP-{Guid.NewGuid()}";
        var request = new CreatePaymentRequest { Amount = 50m, Currency = "USD", ReferenceId = referenceId };

        var firstResponse = await _client.PostAsJsonAsync("/api/payments", request);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();

        var secondResponse = await _client.PostAsJsonAsync("/api/payments", request);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondBody.Should().Be(firstBody);
    }

    [TestMethod]
    public async Task Post_Invalid_Payment_Returns400()
    {
        var token = await GetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePaymentRequest { Amount = -1m, Currency = "USD", ReferenceId = "REF-BAD" };
        var response = await _client.PostAsJsonAsync("/api/payments", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Get_Payment_ById_Returns200()
    {
        var token = await GetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var referenceId = $"REF-GET-{Guid.NewGuid()}";
        var request = new CreatePaymentRequest { Amount = 50m, Currency = "USD", ReferenceId = referenceId };
        var postResponse = await _client.PostAsJsonAsync("/api/payments", request);
        var paymentResponse = await postResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        var getResponse = await _client.GetAsync($"/api/payments/{paymentResponse!.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Get_Payment_NotFound_Returns404()
    {
        var token = await GetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Request_Without_Auth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync($"/api/payments/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Post_With_Same_IdempotencyKey_Returns_Cached_Response()
    {
        var token = await GetTokenAsync();
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreatePaymentRequest { Amount = 50m, Currency = "USD", ReferenceId = $"REF-IK-{Guid.NewGuid()}" };

        using var firstMsg = new HttpRequestMessage(HttpMethod.Post, "/api/payments");
        firstMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        firstMsg.Headers.Add("Idempotency-Key", idempotencyKey);
        firstMsg.Content = JsonContent.Create(request);
        var firstResponse = await _client.SendAsync(firstMsg);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();

        using var secondMsg = new HttpRequestMessage(HttpMethod.Post, "/api/payments");
        secondMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        secondMsg.Headers.Add("Idempotency-Key", idempotencyKey);
        secondMsg.Content = JsonContent.Create(request);
        var secondResponse = await _client.SendAsync(secondMsg);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.Headers.Should().ContainKey("Idempotency-Replay");
        secondBody.Should().Be(firstBody);
    }

    [TestMethod]
    public async Task Post_With_Invalid_IdempotencyKey_Returns400()
    {
        var token = await GetTokenAsync();

        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/payments");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        msg.Headers.Add("Idempotency-Key", "not-a-guid");
        msg.Content = JsonContent.Create(new CreatePaymentRequest { Amount = 50m, Currency = "USD", ReferenceId = $"REF-{Guid.NewGuid()}" });

        var response = await _client.SendAsync(msg);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
