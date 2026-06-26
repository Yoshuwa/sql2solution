namespace MiningFleetOps.Api.CompositionRoot;

public static partial class CustomServiceRegistration
{
    public static IServiceCollection AddCustomApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        OnAddCustomApplicationServices(services, configuration);
        return services;
    }

    static partial void OnAddCustomApplicationServices(IServiceCollection services, IConfiguration configuration);
}