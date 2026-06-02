using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShopAPI.Application.Validators;

namespace ShopAPI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        services.AddAutoMapper(typeof(MappingProfile).Assembly);
        return services;
    }
}
