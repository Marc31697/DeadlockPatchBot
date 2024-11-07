using Discord;
using Discord.WebSocket;
using HtmlAgilityPack;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using DotNetEnv;

class Program
{
    private DiscordSocketClient _client;

    private ulong GuildId;
    private  ulong ChannelId;
		private string BotToken;
    private const string PatchNotesUrl = "https://forums.playdeadlock.com/forums/changelog.10/";

    static async Task Main(string[] args) => await new Program().RunBotAsync();

    public async Task RunBotAsync()
    {
        Console.WriteLine("Initializing bot...");

				DotNetEnv.Env.Load();

        BotToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN") ?? throw new ArgumentNullException("DISCORD_BOT_TOKEN is not set.");
        GuildId = ulong.Parse(Environment.GetEnvironmentVariable("DISCORD_GUILD_ID") ?? throw new ArgumentNullException("DISCORD_GUILD_ID is not set."));
        ChannelId = ulong.Parse(Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID") ?? throw new ArgumentNullException("DISCORD_CHANNEL_ID is not set."));

        _client = new DiscordSocketClient();
        _client.Log += Log;
        _client.Ready += OnReadyAsync;

        await LoginAsync(BotToken);
        await StartPatchNotesCheckLoopAsync();
    }

    private async Task LoginAsync(string token)
    {
        Console.WriteLine("Attempting to log in...");
        await _client.LoginAsync(TokenType.Bot, token);
        Console.WriteLine("Attempting to start the bot...");
        await _client.StartAsync();
        Console.WriteLine("Bot should be connected if no errors.");
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine($"[Discord.Net] {msg}");
        return Task.CompletedTask;
    }

    private Task OnReadyAsync()
    {
        Console.WriteLine("Bot is connected and ready to send messages!");
        return Task.CompletedTask;
    }

		private async Task StartPatchNotesCheckLoopAsync()
    {
        while (true)
        {
            await CheckForPatchNotesAsync();
            await Task.Delay(TimeSpan.FromMinutes(60));
        }
    }

    public static async Task<string> GetLatestPatchNotesLinkAsync()
    {
        Console.WriteLine("Starting web scrape for latest patch notes link...");

        var web = new HtmlWeb();
        HtmlDocument doc;

        try
        {
            doc = await web.LoadFromWebAsync(PatchNotesUrl);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading webpage: {ex.Message}");
            return null;
        }

        var patchNoteLink = doc.DocumentNode
            .SelectNodes("//a")
            ?.FirstOrDefault(a => a.InnerText.Contains("Update"))?
            .GetAttributeValue("href", "");

        Console.WriteLine("Web scrape completed. Patch note link found:");
        Console.WriteLine(patchNoteLink != null ? $"https://forums.playdeadlock.com{patchNoteLink}" : "No link found");

        return patchNoteLink != null ? $"https://forums.playdeadlock.com{patchNoteLink}" : null;
    }

    private async Task CheckForPatchNotesAsync()
    {
        Console.WriteLine("Checking for patch notes...");

        var latestLink = await GetLatestPatchNotesLinkAsync();
				var lastPost = await IsNewPatchNotesLinkAsync(latestLink);
				Console.WriteLine("Here is the last set of patch notes: " + lastPost);
        if (latestLink != null && lastPost)
        {
            Console.WriteLine("Patch notes link found, attempting to send message to Discord channel...");

            if (await TrySendMessageAsync(latestLink))
            {
                Console.WriteLine("Message sent successfully!");
            }
            else
            {
                Console.WriteLine("Failed to send message after multiple attempts.");
            }
        }
        else
        {
            Console.WriteLine("No new patch notes found.");
        }
    }

    private async Task<bool> TrySendMessageAsync(string message)
    {
        const int maxAttempts = 5;
        int attempts = 0;

        while (attempts < maxAttempts)
        {
            attempts++;

            var guild = _client.GetGuild(GuildId);
            if (guild != null)
            {
                var channel = guild.GetTextChannel(ChannelId);
                if (channel != null)
                {
                    try
                    {
                        await channel.SendMessageAsync($"New Patch Notes: {message}");
                        return true; // Message sent successfully
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to send message to channel. Exception: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("Channel not found. Retrying...");
                }
            }
            else
            {
                Console.WriteLine("Guild not found. Retrying...");
            }

            await Task.Delay(1000);
        }

        return false;
    }

		private async Task<bool> IsNewPatchNotesLinkAsync(string latestLink)
		{
				const int maxAttempts = 5;
				int attempts = 0;

				while (attempts < maxAttempts)
				{
						attempts++;

						var guild = _client.GetGuild(GuildId);
						if (guild != null)
						{
								var channel = guild.GetTextChannel(ChannelId);
								if (channel != null)
								{
										var messages = await channel.GetMessagesAsync(1).FlattenAsync(); // Fetch the last message in the channel
										var lastMessage = messages.FirstOrDefault();

										if (lastMessage != null && lastMessage.Content.Contains(latestLink))
										{
												Console.WriteLine("Latest patch notes link has already been posted.");
												return false; // The link is already in the last message
										}

										return true; // Either no messages or the latest link is not in the last message
								}
								else
								{
										Console.WriteLine("Channel not found. Retrying...");
								}
						}
						else
						{
								Console.WriteLine("Guild not found. Retrying...");
						}

						await Task.Delay(1000); // Wait 1 second before retrying
				}

				Console.WriteLine("Failed to find guild or channel after multiple attempts.");
				return true; // Default to true to prevent duplicate attempts if guild/channel couldn't be found
		}

}
