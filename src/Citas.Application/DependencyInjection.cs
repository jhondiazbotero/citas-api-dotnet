using Citas.Application.Abstractions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Citas.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers every use case handler (scoped) and every validator (singleton) of this assembly.
    /// The host still has to provide <see cref="IClinicClock"/>, the three repositories and <see cref="IUnitOfWork"/>.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var types = AssemblyReference.Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (Type type in types)
        {
            foreach (Type contract in type.GetInterfaces())
            {
                if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IValidator<>))
                {
                    services.AddSingleton(contract, type);
                }
            }

            if (type.IsSealed && type.Name.EndsWith("Handler", StringComparison.Ordinal))
            {
                services.AddScoped(type);
            }
        }

        return services;
    }
}
