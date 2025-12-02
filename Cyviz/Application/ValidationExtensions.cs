using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Cyviz.Domain;

namespace Cyviz.Application;

public class SendCommandRequest
{
    public string IdempotencyKey { get; set; } = default!;
    public string Command { get; set; } = default!;
    public Dictionary<string, object>? Parameters { get; set; }
}

public class SendCommandRequestValidator : AbstractValidator<SendCommandRequest>
{
    public SendCommandRequestValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Command)
            .NotEmpty()
            .MaximumLength(100);
    }
}

public static class ValidationExtensions
{
    public static IServiceCollection AddValidators(this IServiceCollection services)
    {
        services.AddScoped<IValidator<Device>, DeviceValidator>();
        services.AddScoped<IValidator<DeviceCommand>, DeviceCommandValidator>();
        return services;
    }
}

public class DeviceValidator : AbstractValidator<Device>
{
    public DeviceValidator()
    {
        RuleFor(d => d.Id).NotEmpty();
        RuleFor(d => d.Name).NotEmpty();
        RuleFor(d => d.Location).MaximumLength(100);
    }
}

public class DeviceCommandValidator : AbstractValidator<DeviceCommand>
{
    public DeviceCommandValidator()
    {
        RuleFor(c => c.DeviceId).NotEmpty();
        RuleFor(c => c.IdempotencyKey).NotEmpty();
        RuleFor(c => c.Command).NotEmpty();
    }
}
