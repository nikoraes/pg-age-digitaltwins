using AgeDigitalTwins.Models;
using Aspire.Hosting;
using System.Text.Json;
using System.Text;

namespace AgeDigitalTwins.ApiService.Test;

public class ModelsIntegrationTests : IAsyncLifetime
{
    private DistributedApplication? _app;
    private HttpClient? _httpClient;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AgeDigitalTwins_AppHost>();
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });
        _app = await appHost.BuildAsync();

        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        await _app.StartAsync();

        await resourceNotificationService.WaitForResourceAsync("apiservice", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));

        _httpClient = _app.CreateHttpClient("apiservice");
    }

    public async Task DisposeAsync()
    {
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
        _httpClient?.Dispose();
    }

    [Fact]
    public async Task Health()
    {
        // Act
        var response = await _httpClient!.GetAsync(
            "/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateModels_SingleModel_ValidatedAndCreated()
    {
        // Arrange
        string[] sModels = [SampleData.DtdlCrater];
        List<JsonElement> jModels = sModels.Select(m => JsonDocument.Parse(m)).Select(j => j.RootElement).ToList();

        // Act
        var response = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(jModels), Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateModels_MultipleDependentModelsResolveInDb_ValidatedAndCreated()
    {
        var delres1 = await _httpClient!.DeleteAsync("/models/dtmi:com:contoso:CelestialBody;1");
        var delres2 = await _httpClient!.DeleteAsync("/models/dtmi:com:contoso:Planet;1");
        var delres3 = await _httpClient!.DeleteAsync("/models/dtmi:com:contoso:Crater;1");

        // Arrange
        string[] sModels = [SampleData.DtdlCelestialBody, SampleData.DtdlCrater];
        List<JsonElement> jModels = sModels.Select(m => JsonDocument.Parse(m)).Select(j => j.RootElement).ToList();

        string[] sModels2 = [SampleData.DtdlPlanet];
        List<JsonElement> jModels2 = sModels2.Select(m => JsonDocument.Parse(m)).Select(j => j.RootElement).ToList();

        // Act
        var response = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(jModels), Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        string responseContent = await response.Content.ReadAsStringAsync();
        var response2 = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(jModels2), Encoding.UTF8, "application/json"));
        response2.EnsureSuccessStatusCode();
        string response2Content = await response2.Content.ReadAsStringAsync();
        List<DigitalTwinsModelData> results = JsonSerializer.Deserialize<List<DigitalTwinsModelData>>(response2Content)!;

        for (int i = 0; i < jModels2.Count; i++)
        {
            var resultJson = JsonDocument.Parse(results[i].DtdlModel);
            var sampleDataJson = jModels2[i];

            var resultId = resultJson.RootElement.GetProperty("@id").GetString();
            var sampleDataId = sampleDataJson.GetProperty("@id").GetString();

            Assert.Equal(sampleDataId, results[i].Id);
            Assert.Equal(sampleDataId, resultId);
        }
    }
}