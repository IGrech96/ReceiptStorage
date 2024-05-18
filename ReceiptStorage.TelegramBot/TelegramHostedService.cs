using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ReceiptStorage.TelegramBot;

public class TelegramHostedService : IHostedService
{
    private readonly IReceiptStorage _storage;
    private readonly ILogger<TelegramHostedService> _logger;
    private readonly IOptions<BotSettings> _options;
    private readonly TelegramBotClient _client;
    private readonly CancellationTokenSource _receivingCancellationTokenSource;

    public TelegramHostedService(
        IReceiptStorage storage,
        ILogger<TelegramHostedService> logger,
        IOptions<BotSettings> options)
    {
        _storage = storage;
        _logger = logger;
        _options = options;
        _client = new TelegramBotClient(_options.Value.Token);
        _receivingCancellationTokenSource = new CancellationTokenSource();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ReceiverOptions receiverOptions = new ()
        {
            
            AllowedUpdates = [
                UpdateType.Message
            ] // receive all update types except ChatMember related updates
        };

        _client.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _receivingCancellationTokenSource.Token
        );

        return Task.CompletedTask;
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient arg1, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram bot error.");
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
        {
            return;
        }

        if (message.From is not { } from || !_options.Value.AcceptedUsers.Contains(from.Id))
        {
            return;
        }

        var fileInfo = message switch
        {
            { Document: not null } => new
            {
                Name = message.Document.FileName,
                FileId = message.Document.FileId,
            },
            _ => null
        };

        if (fileInfo?.FileId is null || fileInfo?.Name is null)
        {
            return;
        }

        await using var memoryStream = new MemoryStream();
        //await botClient.GetInfoAndDownloadFileAsync(fileInfo.FileId, memoryStream, cancellationToken);
        await botClient.GetInfoAndDownloadFileAsync(
            fileId: fileInfo.FileId,
            destination: memoryStream,
            cancellationToken: cancellationToken);
        memoryStream.Position = 0;

        var info = await _storage.Handle(memoryStream, fileInfo.Name);
        if (info.Status is ReceiptHandleResponseStatus.UnrecognizedFormat)
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                $"Unrecognized file format.",
                replyToMessageId: message.MessageId,
                cancellationToken: cancellationToken
            );
        }
        if (info.Status is ReceiptHandleResponseStatus.UnknowError)
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                $"Unknow error.",
                replyToMessageId: message.MessageId,
                cancellationToken: cancellationToken
            );
        }

        if (info.Success)
        {
            var responseText = new StringBuilder();
            responseText.Append(Escape(info.FileName)).AppendLine(":").AppendLine();

            foreach (var (name,data) in info.Details)
            {
                responseText.Append("_").Append(Escape(name)).Append(": ").Append(Escape(data)).AppendLine("_");
            }


            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                responseText.ToString(),
                parseMode:ParseMode.MarkdownV2,
                replyToMessageId: message.MessageId,
                cancellationToken: cancellationToken
            );
        }

        string Escape(string value) => value.Replace(".", "\\.").Replace("_", "\\_");

    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _receivingCancellationTokenSource.Cancel();

        return Task.CompletedTask;
    }
}