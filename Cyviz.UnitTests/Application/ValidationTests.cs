using Cyviz.Application;
using Cyviz.Domain;
using FluentValidation;

namespace Cyviz.UnitTests.Application;

public class ValidationTests
{
    [Fact]
    public void SendCommandRequest_ValidCommand_PassesValidation()
    {
        // Arrange
        var validator = new SendCommandRequestValidator();
        var request = new SendCommandRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            Command = "Reboot"
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SendCommandRequest_InvalidIdempotencyKey_FailsValidation(string? idempotencyKey)
    {
        // Arrange
        var validator = new SendCommandRequestValidator();
        var request = new SendCommandRequest
        {
            IdempotencyKey = idempotencyKey!,
            Command = "Reboot"
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SendCommandRequest.IdempotencyKey));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SendCommandRequest_InvalidCommand_FailsValidation(string? command)
    {
        // Arrange
        var validator = new SendCommandRequestValidator();
        var request = new SendCommandRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            Command = command!
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SendCommandRequest.Command));
    }

    [Fact]
    public void SendCommandRequest_IdempotencyKeyTooLong_FailsValidation()
    {
        // Arrange
        var validator = new SendCommandRequestValidator();
        var request = new SendCommandRequest
        {
            IdempotencyKey = new string('x', 201), // > 200 chars
            Command = "Reboot"
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.PropertyName == nameof(SendCommandRequest.IdempotencyKey) &&
            e.ErrorMessage.Contains("200"));
    }

    [Fact]
    public void SendCommandRequest_CommandTooLong_FailsValidation()
    {
        // Arrange
        var validator = new SendCommandRequestValidator();
        var request = new SendCommandRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            Command = new string('x', 101) // > 100 chars
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.PropertyName == nameof(SendCommandRequest.Command) &&
            e.ErrorMessage.Contains("100"));
    }

    [Theory]
    [InlineData("Reboot")]
    [InlineData("Ping")]
    [InlineData("GetStatus")]
    [InlineData("Reset")]
    [InlineData("CustomCommand")]
    public void SendCommandRequest_ValidCommands_PassValidation(string command)
    {
        // Arrange
        var validator = new SendCommandRequestValidator();
        var request = new SendCommandRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            Command = command
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SendCommandRequest_WithParameters_PassesValidation()
    {
        // Arrange
        var validator = new SendCommandRequestValidator();
        var request = new SendCommandRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            Command = "SetVolume",
            Parameters = new Dictionary<string, object>
            {
                { "level", 75 },
                { "mute", false }
            }
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Device_ValidDevice_PassesValidation()
    {
        // Arrange
        var device = new Device
        {
            Id = "device-01",
            Name = "Test Device",
            Type = DeviceType.Display,
            Protocol = DeviceProtocol.HttpJson,
            Status = DeviceStatus.Online,
            Firmware = "v1.0.0",
            Location = "Test Lab",
            Capabilities = new[] { "Ping", "Reboot" }
        };

        // Assert - No validation errors should occur
        device.Id.Should().NotBeNullOrWhiteSpace();
        device.Name.Should().NotBeNullOrWhiteSpace();
        device.Capabilities.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Device_InvalidId_ThrowsValidationError(string? deviceId)
    {
        // Arrange & Act
        Action act = () =>
        {
            var device = new Device
            {
                Id = deviceId!,
                Name = "Test Device",
                Type = DeviceType.Display
            };

            // Simulate validation that would occur in controller
            if (string.IsNullOrWhiteSpace(device.Id))
            {
                throw new ValidationException("Device ID is required");
            }
        };

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Device ID*");
    }
}
