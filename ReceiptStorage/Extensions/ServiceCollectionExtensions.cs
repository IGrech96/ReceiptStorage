using Microsoft.Extensions.DependencyInjection;
using ReceiptStorage.Storages;
using ReceiptStorage.Templates;

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

    public static IServiceCollection AddDefaultTemplates(this IServiceCollection services)
    {
        return services
            .AddSingleton<IPdfTemplate, MtbankTemplate>()
            .AddSingleton<IPdfTemplate, HouseCommunalTemplate>()
            .AddSingleton<IPdfTemplate, HouseCommunalTemplate2>();
    }
}