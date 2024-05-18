using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReceiptStorage.TelegramBot;

public class BotSettings
{
    public required string Token { get; set; }

    public required long[] AcceptedUsers { get; set; } = [];
}