namespace Cyviz.IntegrationTests.Api;

public class HealthCheckTests : IClassFixture<CyvizWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(CyvizWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<HealthResponse>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("ok");
    }

    private class HealthResponse
    {
        public string Status { get; set; } = null!;
    }
}
