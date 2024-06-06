using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReceiptStorage;
using ReceiptStorage.Extensions;
using ReceiptStorage.Storages;
using ReceiptStorage.Tags;
using ReceiptStorage.TelegramBot;


var host = Host.CreateApplicationBuilder();
host.Services.AddHostedService<TelegramHostedService>();
host.Services.Configure<BotSettings>(host.Configuration.GetSection(nameof(BotSettings)));
host.Services.Configure<PgStorageSettings>(host.Configuration.GetSection(nameof(PgStorageSettings)));
host.Services.Configure<FileStorageSettings>(host.Configuration.GetSection(nameof(FileStorageSettings)));
host.Services.Configure<TagResolverSettings>(host.Configuration.GetSection(nameof(TagResolverSettings)));
host.Services.AddSingleton<IReceiptParser, ReceiptStorage.ReceiptParser>();
host.Services.AddDefaultTemplates();
host.Services.AddDefaultStorages();
host.Services.AddSingleton<ITagResolver, TagResolver>();

await host.Build().RunAsync();