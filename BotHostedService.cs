
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace Lesson26;

public class BotHostedService(
    ILogger<BotHostedService> logger,
    ITelegramBotClient botClient,
    IUpdateHandler updateHandler) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var me = await botClient.GetMe(cancellationToken);
        logger.LogInformation("🎉 {bot} has started successfully.", $"{me.FirstName} - {me.Username}");

        var receiverOptions = new ReceiverOptions
        {
            DropPendingUpdates = true,
            AllowedUpdates = Array.Empty<UpdateType>() 
        };

        botClient.StartReceiving(
            updateHandler: updateHandler,
            receiverOptions: receiverOptions,
            cancellationToken: cancellationToken);

        logger.LogInformation("🤖 Bot qabul qilishni boshladi...");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{service} is exiting...", nameof(BotHostedService));
        return Task.CompletedTask;
    }
}
