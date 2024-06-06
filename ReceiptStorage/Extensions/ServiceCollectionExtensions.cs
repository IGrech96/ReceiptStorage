using Microsoft.Extensions.DependencyInjection;
using ReceiptStorage.Storages;

namespace ReceiptStorage.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDefaultStorages(this IServiceCollection services)
    {
        return services
            .AddSingleton<IReceiptStorage, CompositeStorage>()
            .AddKeyedSingleton<IReceiptStorage, PgStorage>("DB")
            .AddKeyedSingleton<IReceiptStorage, FileStorage>("File");
    }
}