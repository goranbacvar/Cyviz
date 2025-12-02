namespace Cyviz.IntegrationTests.Api;

public class ApiKeyAuthenticationTests : IClassFixture<CyvizWebApplicationFactory>
{
    private readonly CyvizWebApplicationFactory _factory;

    public ApiKeyAuthenticationTests(CyvizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ApiRequest_WithoutApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        // Don't add API key header

        // Act
        var response = await client.GetAsync("/api/devices");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApiRequest_WithInvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "invalid-key");

        // Act
        var response = await client.GetAsync("/api/devices");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApiRequest_WithValidApiKey_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "local-dev-key");

        // Act
        var response = await client.GetAsync("/api/devices");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_WithoutApiKey_ReturnsOk()
    {
        // Arrange - Health endpoint should not require API key
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BlazorPage_WithoutApiKey_ReturnsOk()
    {
        // Arrange - Blazor pages should not require API key
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
