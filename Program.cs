
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Lesson26;
using Telegram.Bot.Polling; 

var builder = Host.CreateApplicationBuilder();

builder.Services.AddSingleton<ITelegramBotClient>(provider =>
{
    var token = builder.Configuration["Bot:Token"];
    return new TelegramBotClient(token!);
});

builder.Services.AddSingleton<IUpdateHandler, BotUpdateHandler>();

builder.Services.AddSingleton<IHostedService, BotHostedService>();

await builder.Build().RunAsync();

