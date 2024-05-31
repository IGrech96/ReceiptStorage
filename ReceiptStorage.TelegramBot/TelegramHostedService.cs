using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ReceiptStorage.TelegramBot;

public class TelegramHostedService : IHostedService
{
    const string RetryCallbackData = "Retry";

    private readonly IReceiptStorageHandler _storageHandler;
    private readonly ILogger<TelegramHostedService> _logger;
    private readonly IOptions<BotSettings> _options;
    private readonly TelegramBotClient _client;
    private readonly CancellationTokenSource _receivingCancellationTokenSource;

    public TelegramHostedService(
        IReceiptStorageHandler storageHandler,
        ILogger<TelegramHostedService> logger,
        IOptions<BotSettings> options)
    {
        _storageHandler = storageHandler;
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
                UpdateType.Message,
                UpdateType.CallbackQuery
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
        if (update.Message is  { } message)
        {
            await HandleMessage(botClient, message, cancellationToken);
        }

        if (update.CallbackQuery is { } callback)
        {
            await HandleCallback(botClient, callback, cancellationToken);
        }
      
    }

    private async Task HandleCallback(ITelegramBotClient botClient, CallbackQuery callback, CancellationToken cancellationToken)
    {
        if (callback is { Data: RetryCallbackData, Message: { ReplyToMessage: {} sourceMessage } })
        {
            await HandleMessage(botClient, sourceMessage, cancellationToken);
            await botClient.AnswerCallbackQueryAsync(callback.Id, cancellationToken: cancellationToken);
        }
    }

    private async Task HandleMessage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
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

        _logger.LogInformation($"Request '{fileInfo.Name}' received.");
        await using var memoryStream = new MemoryStream();

        await botClient.GetInfoAndDownloadFileAsync(
            fileId: fileInfo.FileId,
            destination: memoryStream,
            cancellationToken: cancellationToken);
        memoryStream.Position = 0;

        var onErrorKeyboard = new InlineKeyboardMarkup(
            new[]
            {
                new InlineKeyboardButton("Retry") { CallbackData = RetryCallbackData }
            });


        var info = await _storageHandler.Handle(new Content(fileInfo.Name, memoryStream), new TelegramUser(from), cancellationToken);
        if (info.Status is ReceiptHandleResponseStatus.UnrecognizedFormat)
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                $"Unrecognized file format.",
                replyToMessageId: message.MessageId,
                replyMarkup: onErrorKeyboard,
                cancellationToken: cancellationToken
            );
        }
        if (info.Status is ReceiptHandleResponseStatus.UnknowError)
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                $"Unknow error.",
                replyToMessageId: message.MessageId,
                replyMarkup: onErrorKeyboard,
                cancellationToken: cancellationToken
            );
        }

        if (info.Success)
        {
            var responseText = new StringBuilder();
            responseText
                .Append("\\(")
                .Append(Escape(info.Details.Value.Type))
                .Append("\\) ")
                .Append(Escape(info.Details.Value.Title))
                .Append(" ")
                .Append(Escape(info.Details.Value.Timestamp.ToString("yyyy-MM-dd")))
                .AppendLine(":")
                .AppendLine();

            foreach (var (name,data) in info.Details.Value.Details)
            {
                responseText.Append("_").Append(Escape(name)).Append(": ").Append(Escape(data)).AppendLine("_");
            }

            responseText.AppendLine();

            responseText.AppendLine(string.Join(", ", info.Details.Value.Tags.Select(t => Escape("#" + t))));


            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                responseText.ToString(),
                parseMode:ParseMode.MarkdownV2,
                replyToMessageId: message.MessageId,
                cancellationToken: cancellationToken
            );
        }

        _logger.LogInformation($"Request '{fileInfo.Name}' completed.");

        string Escape(string value) => value
            .Replace(".", "\\.")
            .Replace("_", "\\_")
            .Replace("-", "\\-")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("#", "\\#");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _receivingCancellationTokenSource.Cancel();

        return Task.CompletedTask;
    }

    private class TelegramUser(User user) : IUser
    {
        private readonly User _user = user;
    }
}