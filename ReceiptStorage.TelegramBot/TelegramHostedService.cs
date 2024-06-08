using System.IO;
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
    const string GetSourceCallbackData = "GetSource";

    private readonly IReceiptParser _parser;
    private readonly IReceiptStorage _storage;
    private readonly ILogger<TelegramHostedService> _logger;
    private readonly IOptions<BotSettings> _options;
    private readonly TelegramBotClient _client;
    private readonly CancellationTokenSource _receivingCancellationTokenSource;

    public TelegramHostedService(
        IReceiptParser parser,
        IReceiptStorage storage,
        ILogger<TelegramHostedService> logger,
        IOptions<BotSettings> options)
    {
        _parser = parser;
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
                UpdateType.Message,
                UpdateType.CallbackQuery,
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
        try
        {
            if (update.Message is { ReplyToMessage: {} replyToMessage } message1)
            {
                await AddTagsToMessage(botClient, message1, replyToMessage, cancellationToken);
                await botClient.DeleteMessageAsync(new ChatId(message1.Chat.Id), message1.MessageId, cancellationToken: cancellationToken);
            }

            if (update.Message is { } message2)
            {
                await HandleMessage(botClient, message2, cancellationToken);
            }

            if (update.CallbackQuery is { } callback)
            {
                await HandleCallback(botClient, callback, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await HandlePollingErrorAsync(botClient, ex, cancellationToken);
        }
    }

    private async Task HandleCallback(ITelegramBotClient botClient, CallbackQuery callback, CancellationToken cancellationToken)
    {
        if (callback is { Data: RetryCallbackData, Message: { ReplyToMessage: {} messageToRetry } })
        {
            await HandleMessage(botClient, messageToRetry, cancellationToken);
            await botClient.AnswerCallbackQueryAsync(callback.Id, cancellationToken: cancellationToken);
        }

        if (callback is { Data: GetSourceCallbackData, Message:{} sourceMessage })
        {
            var content = await _storage.TryGetContentByExternalIdAsync(sourceMessage.MessageId, cancellationToken);
            if (content != null)
            {
                await botClient.SendDocumentAsync(
                    chatId: new ChatId(sourceMessage.Chat.Id),
                    InputFile.FromStream(content.GetStream(), content.Name),
                    replyToMessageId: sourceMessage.MessageId,
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId: new ChatId(sourceMessage.Chat.Id),
                    "Source not found",
                    replyToMessageId: sourceMessage.MessageId,
                    cancellationToken: cancellationToken
                );
            }
            await botClient.AnswerCallbackQueryAsync(callback.Id, cancellationToken: cancellationToken);

        }
    }

    private async Task AddTagsToMessage(ITelegramBotClient botClient, Message messageWithTags, Message messageToEdit, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Request to edit tags '{messageToEdit.MessageId}' received.");

        var newTags = messageWithTags
            .Text!
            .ReplaceLineEndings(" ")
            .Replace(",", " ")
            .Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.TrimStart('#'))
            .Distinct()
            .ToArray();

        var messageLines = messageToEdit.Text!.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        var responseText = new StringBuilder();

        if (messageLines.Length > 1)
        {
            responseText.AppendLine(Escape(messageLines[0]));
            responseText.AppendLine(Escape(messageLines[1]));
        }

        bool hasSourceTags = messageLines.Last().StartsWith("#");

        var linesToKeep = hasSourceTags ? messageLines.Length - 1 : messageLines.Length;
        for (int index = 2; index < linesToKeep; index++)
        {
            if (string.IsNullOrEmpty(messageLines[index]))
            {
                responseText.AppendLine();
            }
            else
            {
                responseText.Append("_").Append(Escape(messageLines[index])).Append("_").AppendLine();
            }
        }

        if (hasSourceTags)
        {
            responseText.Append(Escape(messageLines.Last()));
            responseText.Append(", ");
        }

        responseText.AppendLine(string.Join(", ", newTags.Select(t => Escape("#" + t))));

        var sentMessage = await botClient.EditMessageTextAsync(
            chatId: new ChatId(messageToEdit.Chat.Id),
            parseMode:ParseMode.MarkdownV2,
            messageId:messageToEdit.MessageId,
            text:responseText.ToString(),
            cancellationToken: cancellationToken
        );


        _logger.LogInformation($"Request to edit tags '{messageToEdit.MessageId}' completed.");

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

        var onSuccessKeyboard = new InlineKeyboardMarkup(
            new[]
            {
                new InlineKeyboardButton("Get Source") { CallbackData = GetSourceCallbackData }
            });


        var content = new Content(fileInfo.Name, memoryStream);
        var info = await _parser.Parse(content, cancellationToken);
        if (info.Status is ReceiptParserResponseStatus.UnrecognizedFormat)
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                $"Unrecognized file format.",
                replyToMessageId: message.MessageId,
                replyMarkup: onErrorKeyboard,
                cancellationToken: cancellationToken
            );
        }
        if (info.Status is ReceiptParserResponseStatus.UnknowError)
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
            var responceBuilder = InfoToText(info);

            var finalText = responceBuilder.ToString();

            responceBuilder.AppendLine("_Processing_");

            var sentMessage = await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                responceBuilder.ToString(),
                parseMode:ParseMode.MarkdownV2,
                replyMarkup: onSuccessKeyboard,
                cancellationToken: cancellationToken
            );

            var details = info.Details.Value;
            details.ExternalId = sentMessage.MessageId;

            await _storage.SaveAsync(content, details, new TelegramUser(from), cancellationToken);

            await botClient.EditMessageTextAsync(
                chatId: new ChatId(message.Chat.Id),
                messageId: sentMessage.MessageId,
                text: finalText,
                replyMarkup:onSuccessKeyboard,
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: cancellationToken);

            await botClient.DeleteMessageAsync(
                chatId: new ChatId(message.Chat.Id),
                messageId: message.MessageId,
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation($"Request '{fileInfo.Name}' completed.");
    }

    private static StringBuilder InfoToText(ReceiptParserResponse info)
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
        return responseText;
    }

    private static string Escape(string value) =>
        value
            .Replace(".", "\\.")
            .Replace("_", "\\_")
            .Replace("-", "\\-")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("#", "\\#");

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