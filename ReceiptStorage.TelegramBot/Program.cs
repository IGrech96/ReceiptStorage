using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReceiptStorage;
using ReceiptStorage.TelegramBot;
using ReceiptStorage.Templates;
using Telegram.Bot;


var host = Host.CreateApplicationBuilder();
host.Services.AddHostedService<TelegramHostedService>();
host.Services.Configure<BotSettings>(host.Configuration.GetSection(nameof(BotSettings)));
host.Services.Configure<PgStorageSettings>(host.Configuration.GetSection(nameof(PgStorageSettings)));
host.Services.Configure<FileStorageSettings>(host.Configuration.GetSection(nameof(FileStorageSettings)));
host.Services.AddSingleton<IReceiptStorageHandler, ReceiptStorage.ReceiptStorageHandler>();
host.Services.AddSingleton<IPdfTemplate, MtbankTemplate>();
host.Services.AddSingleton<IReceiptStorage, PgStorage>();
host.Services.AddSingleton<IReceiptStorage, FileStorage>();

await host.Build().RunAsync();