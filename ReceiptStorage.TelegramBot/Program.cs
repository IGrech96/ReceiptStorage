using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReceiptStorage;
using ReceiptStorage.TelegramBot;
using ReceiptStorage.Templates;
using Telegram.Bot;


var host = Host.CreateApplicationBuilder();
host.Services.AddHostedService<TelegramHostedService>();
host.Services.Configure<BotSettings>(host.Configuration.GetSection(nameof(BotSettings)));
host.Services.AddSingleton<IReceiptStorageHandler, ReceiptStorage.ReceiptStorageHandler>();
host.Services.AddSingleton<IPdfTemplate, MtbankTemplate>();

await host.Build().RunAsync();