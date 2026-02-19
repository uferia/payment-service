using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PaymentService.Api.Models;

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

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    public async Task Post_Duplicate_Payment_Returns200()
    {
        var token = await GetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var referenceId = $"REF-DUP-{Guid.NewGuid()}";
        var request = new CreatePaymentRequest { Amount = 50m, Currency = "USD", ReferenceId = referenceId };

        await _client.PostAsJsonAsync("/api/payments", request);
        var response = await _client.PostAsJsonAsync("/api/payments", request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task Post_Invalid_Payment_Returns400()
    {
        var token = await GetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePaymentRequest { Amount = -1m, Currency = "USD", ReferenceId = "REF-BAD" };
        var response = await _client.PostAsJsonAsync("/api/payments", request);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
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

        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [TestMethod]
    public async Task Get_Payment_NotFound_Returns404()
    {
        var token = await GetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Request_Without_Auth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync($"/api/payments/{Guid.NewGuid()}");
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
