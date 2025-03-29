using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Managers;
using ACE.Server.Entity;
using System.Timers;
using System.Collections.Concurrent;
using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

namespace ACE.Server.InfinitideMods
{
    public class DiscordRelay
    {
        //TurbineChatHandler.cs and GameMessageTurbineChat.cs are what's relevant
        //Todo: Filtering relevant messages.  White/blacklisting.

        //Supply credentials
        private static ulong RELAY_CHANNEL_ID = 0;
        private static string BOT_TOKEN = "";
        private static int QUIET_START = 0;

        private static DiscordSocketClient discord;

        private static IMessageChannel channel;

        //Outgoing messages
        private static ConcurrentQueue<string> outgoingMessages;
        private static Timer messageTimer;
        private const int MAX_MESSAGE_LENGTH = 10000;
        private const double MESSAGE_INTERVAL = 10000;
        private const string PREFIX = "~";
        private static bool IsInitialized = false;

        //Initialize in Program.cs or on first use?
        public async static void Initialize()
        {
            if (IsInitialized)
                return;

            LoadConfiguration();

            //Set up outgoing message queue
            outgoingMessages = new ConcurrentQueue<string>();

            messageTimer = new Timer
            {
                AutoReset = true,
                Enabled = false,
                Interval = MESSAGE_INTERVAL,
            };

            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent  // Required for accessing message content
            };

            messageTimer.Elapsed += SendQueuedMessages;

            discord = new DiscordSocketClient(config);
            await discord.LoginAsync(TokenType.Bot, BOT_TOKEN);
            await discord.StartAsync();
            discord.Ready += OnReady;
        }

        private static void LoadConfiguration()
        {
            //Create config file if it doesn't exist
            if (!File.Exists("DiscordConfig.json"))
            {
                dynamic config = new
                {
                    relay_channel_id = 0,
                    bot_token = "",
                    quiet_start = 0
                };
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText("DiscordConfig.json", json);
                Console.WriteLine("DiscordConfig.json created. Please fill in the required fields and restart the server.");
            }
            try
            {
                using (StreamReader r = new StreamReader("DiscordConfig.json"))
                {
                    string json = r.ReadToEnd();
                    dynamic config = JsonConvert.DeserializeObject(json);
                    RELAY_CHANNEL_ID = config.relay_channel_id;
                    BOT_TOKEN = config.bot_token;
                    QUIET_START = config.quiet_start;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading Discord configuration: " + ex.Message);
            }
        }

        //Finish initializing when logged in to Discord
        private static Task OnReady()
        {
            if (IsInitialized)
                return Task.CompletedTask;

            //Grab the channel to be used for relaying messages
            channel = discord.GetChannel(RELAY_CHANNEL_ID) as IMessageChannel;

            if (channel == null)
                return Task.CompletedTask;

            //Set up relay
            discord.MessageReceived += OnDiscordChat;

            //Start ACE-->Discord timer
            messageTimer.Enabled = true;

            //Say hi
            if (QUIET_START < 1)
                QueueMessageForDiscord("Shattered Dawn Chat Relay.");

            IsInitialized = true;

            return Task.CompletedTask;
        }

        //Batch messages going to Discord to help with rate limits
        private static void SendQueuedMessages(object sender, ElapsedEventArgs e)
        {
            if (channel is null)
                return;

            var batchedMessage = new StringBuilder();

            while (batchedMessage.Length < MAX_MESSAGE_LENGTH &&
                outgoingMessages.TryDequeue(out string message))
            {
                batchedMessage.AppendLine(message);
            }

            Task.Run(async () =>
            {
                await channel.SendMessageAsync(batchedMessage.ToString());
            });
        }

        //Relay messages from Discord
        private static Task OnDiscordChat(SocketMessage msg)
        {
            //Ignore bot chat and incorrect channels
            if (msg.Author.IsBot || msg.Channel.Id != RELAY_CHANNEL_ID)
                return Task.CompletedTask;

            //Check if the server has disabled general chat
            if (PropertyManager.GetBool("chat_disable_general").Item)
                return Task.CompletedTask;

            if (msg.Content.Length == 0)
                return Task.CompletedTask;

            if (msg.Content is not string)
                return Task.CompletedTask;

            string input = msg.Content;
            string pattern = $"{"<"}.*?{">"}{" "}";
            string newPatern = Regex.Replace(input, pattern, string.Empty);

            //Limit the number of characters to 222 to prevent overflow
            if (newPatern.Length > 222)
                newPatern = newPatern.Substring(0, 222); ;

            var guildUser = msg.Author as SocketGuildUser;
            var authorName = msg.Author.Username;
            var prefix = PREFIX;

            if (guildUser != null)
            {
                authorName = guildUser.Nickname ?? guildUser.Username;

                var roles = guildUser.Roles;

                // Example: Check if the user has a specific role by name
                bool hasAdminRole = roles.Any(role => role.Name == "Rift Master");

                if (hasAdminRole)
                {
                    prefix = "*";
                }
                else
                    prefix = "~";
            }

            //Construct message
            var chatMessage = new GameMessageTurbineChat(ChatNetworkBlobType.NETBLOB_EVENT_BINARY, ChatNetworkBlobDispatchType.ASYNCMETHOD_SENDTOROOMBYNAME, TurbineChatChannel.General, prefix + authorName, newPatern, 0, ChatType.General);

            //Send a message to any player who is listening to general chat
            foreach (var recipient in PlayerManager.GetAllOnline())
            {
                // handle filters
                if (!recipient.GetCharacterOption(CharacterOption.ListenToGeneralChat))
                    return Task.CompletedTask;

                //Todo: think about how to handle squelches?
                //if (recipient.SquelchManager.Squelches.Contains(session.Player, ChatMessageType.AllChannels))
                //    continue;

                recipient.Session.Network.EnqueueSend(chatMessage);
            }

            return Task.CompletedTask;
        }

        //Called when a GameMessageTurbineChat is created to see if it should be sent to Discord
        public static void RelayIngameChat(string message, string senderName, ChatType chatType, uint channel, uint senderID, ChatNetworkBlobType chatNetworkBlobType, ChatNetworkBlobDispatchType chatNetworkBlobDispatchType)
        {
            if (message is null || senderName is null)
                return;
            if (senderName.StartsWith("*"))
                return;
            if (senderName.StartsWith("~"))
                return;

            if (chatType == ChatType.General || chatType == ChatType.LFG)
                QueueMessageForDiscord($"[{chatType}] {senderName}: {message}");
        }

        public static void QueueMessageForDiscord(string message)
        {
            outgoingMessages.Enqueue(message);
        }
    }
}

