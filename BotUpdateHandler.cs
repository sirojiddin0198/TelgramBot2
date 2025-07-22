using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Lesson26;

public class BotUpdateHandler(ILogger<BotUpdateHandler> logger) : IUpdateHandler
{
    private readonly Dictionary<long, UserSession> _sessions = new();

    private static readonly Dictionary<string, string> ColorNameToHex = new(StringComparer.OrdinalIgnoreCase)
    {
        { "qizil", "#FF0000" },
        { "yashil", "#00FF00" },
        { "moviy", "#0000FF" },
        { "sariq", "#FFFF00" },
        { "qora", "#000000" },
        { "oq", "#FFFFFF" },
        { "pushti", "#FFC0CB" },
        { "kulrang", "#808080" },
        { "havorang", "#00FFFF" },
        { "to'q yashil", "#006400" }
    };

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Xatolik yuz berdi: {msg}", exception.Message);
        return Task.CompletedTask;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        
        if (update.Type == UpdateType.Message && update.Message!.Text is { } text)
        {
            logger.LogInformation("💌 Yangi xabar: {client} ({id}) dan keldi", update.Message?.Chat.FirstName, update.Message?.Chat.Username);
            var chatId = update.Message!.Chat.Id;
            var session = GetOrCreateSession(chatId);

            if (session.ExpectingHtmlColor)
            {
                if (TryParseColor(text, out var hex))
                {
                    session.HtmlColor = hex;
                    session.ExpectingHtmlColor = false;
                    session.ExpectingSeed = true;
                    await botClient.SendMessage(chatId, "🧬 Ixtiyoriy *seed* kiriting (harflar/sonlar):", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                }
                else
                {
                    var examples = string.Join(", ", ColorNameToHex.Keys.Take(5).Select(x => $"`{x}`"));
                    var message = "❌ Noto'g'ri HTML rang kodi yoki rang nomi.\n" +
                                  "Masalan: `#ff5733`, `qizil`, `yashil`\n" +
                                  $"Yordam uchun: {examples} ...";
                    await botClient.SendMessage(chatId, message, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                }
                return;
            }

            if (session.ExpectingSeed)
            {
                session.Seed = text.Trim();
                await SendAvatar(botClient, chatId, session, cancellationToken);
                _sessions.Remove(chatId);
                return;
            }

            if (text == "/start")
            {
                _sessions.Remove(chatId);
                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🎉 Fun Emoji", "style:fun-emoji"),
                        InlineKeyboardButton.WithCallbackData("🖼️ Avataaars", "style:avataaars")
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🤖 Bottts", "style:bottts"),
                        InlineKeyboardButton.WithCallbackData("🎨 Pixel Art", "style:pixel-art")
                    }
                });

                await botClient.SendMessage(chatId, text:"Avatar uslubini tanlang:", replyMarkup: keyboard, cancellationToken: cancellationToken);

            }
        }
        else if (update.Type == UpdateType.CallbackQuery)
        {
            var callback = update.CallbackQuery!;
            var chatId = callback.Message!.Chat.Id;
            var data = callback.Data!;
            var session = GetOrCreateSession(chatId);

            await botClient.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);

            if (data.StartsWith("style:"))
            {
                session.Style = data.Split(':')[1];
                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🖼 PNG", "format:png"),
                        InlineKeyboardButton.WithCallbackData("🧾 SVG", "format:svg")
                    }
                });
                // await botClient.EditMessageText(chatId, callback.Message.MessageId, null, cancellationToken: cancellationToken);
                // await botClient.SendMessage(chatId, text: "Qaysi formatda yuboraylik?", replyMarkup: keyboard, cancellationToken: cancellationToken);
                await botClient.EditMessageText(
                    chatId: chatId,
                    messageId: callback.Message.MessageId,
                    text: "Qaysi formatda yuboraylik?",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);


            }
            else if (data.StartsWith("format:"))
            {
                session.Format = data.Split(':')[1];
                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🔳 Transparent", "bg:transparent"),
                        InlineKeyboardButton.WithCallbackData("🔲 Solid", "bg:solid")
                    }
                });
                // await botClient.EditMessageReplyMarkup(chatId, callback.Message.MessageId, null, cancellationToken: cancellationToken);
                // await botClient.SendMessage(chatId, text: "Fon qanday bo'lsin?", replyMarkup: keyboard, cancellationToken: cancellationToken);
                await botClient.EditMessageText(
                    chatId: chatId,
                    messageId: callback.Message.MessageId,
                    text: "Fon qanday bo'lsin?",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);

            }
            else if (data.StartsWith("bg:"))
            {
                session.BackgroundType = data.Split(':')[1];
                await botClient.EditMessageReplyMarkup(chatId, callback.Message.MessageId, null, cancellationToken: cancellationToken);
                if (session.BackgroundType == "transparent")
                {
                    session.Seed = Guid.NewGuid().ToString("N")[..6];
                    await SendAvatar(botClient, chatId, session, cancellationToken);
                    _sessions.Remove(chatId);
                }
                else
                {
                    session.ExpectingHtmlColor = true;

                    await botClient.SendMessage(chatId, text: "🎨 HTML rang kodi yoki rang nomini kiriting (masalan: `#34eb92`, `qizil`):", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                }
            }
        }
    }

    private async Task SendAvatar(ITelegramBotClient botClient, long chatId, UserSession session, CancellationToken ct)
    {
        var seed = session.Seed ?? Guid.NewGuid().ToString("N")[..6];
        var baseUrl = $"https://api.dicebear.com/8.x/{session.Style}/{session.Format}?seed={seed}";

        if (session.Format == "png" && session.BackgroundType == "solid")
        {
            baseUrl += $"&backgroundColor={session.HtmlColor.TrimStart('#')}";
        }

        var caption = $"🎭 *{session.Style}* uslubi\n💾 *{session.Format.ToUpper()}*\n🧬 Seed: `{seed}`";

        if (session.Format == "png")
        {
            await botClient.SendPhoto(chatId, baseUrl, caption, parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
        else
        {
            using var http = new HttpClient();
            var bytes = await http.GetByteArrayAsync(baseUrl, ct);
            using var stream = new MemoryStream(bytes);
            var file = new InputFileStream(stream, $"avatar-{seed}.svg");

            await botClient.SendDocument(chatId, file, caption, parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
    }

    private bool TryParseColor(string input, out string hex)
    {
        input = input.Trim().ToLower();

        if (ColorNameToHex.TryGetValue(input, out hex!))
            return true;

        if (Regex.IsMatch(input, @"^#([0-9a-fA-F]{6})$"))
        {
            hex = input;
            return true;
        }

        hex = "";
        return false;
    }

    private UserSession GetOrCreateSession(long chatId)
    {
        if (!_sessions.TryGetValue(chatId, out var session))
        {
            session = new UserSession();
            _sessions[chatId] = session;
        }
        return session;
    }

    private class UserSession
    {
        public string Style = "";
        public string Format = "";
        public string BackgroundType = "";
        public string HtmlColor = "";
        public string Seed = "";
        public bool ExpectingHtmlColor = false;
        public bool ExpectingSeed = false;
    }
}
