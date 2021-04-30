using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using AutoUpdaterDotNET;
using System.Xml;

namespace Jay_Bot
{
    class Program
    {
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        private DiscordSocketClient _client;
        public static int messageCount;
        public static Random rng = new Random();
        public static string previousmessage;
        static Dictionary<ulong, int> reacts = new Dictionary<ulong, int>();
        static Dictionary<ulong, bool> isPinned = new Dictionary<ulong, bool>();
        public static int fireAt = 20;


        //Load from xml
        public static XmlDocument settings = new XmlDocument();
        public static ulong adminId;
        public static int reactLimit;
        public static string reactEmote;
        public static string memoryFile;
        public static ulong pinChannel;
        public static List<UInt64> blacklist = new List<ulong>();
        public static Dictionary<string, string> quotes = new Dictionary<string, string>();
        public static string rules;


        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            _client.Log += Log;
            XmlDocument settings = new XmlDocument();
            loadSettings();
            
            StreamReader rulesText = new StreamReader(@"rules.txt");
            rules = rulesText.ReadToEnd();
            rulesText.Dispose();

            StreamReader rToke = new StreamReader(@"token.txt"); //login token
            string tokenRead = rToke.ReadToEnd();
            var token = tokenRead;
            rToke.Dispose();

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();


            _client.MessageReceived += MessageReceived;//events
            _client.ReactionAdded += CountReacts;
            _client.ReactionRemoved += removeReactCount;

            StreamReader sr = new StreamReader(memoryFile, Encoding.ASCII);//markov memory
            string training = sr.ReadToEnd();
            Markov.markovTrain(training);
            sr.Dispose();

            rngNumber();

            AutoUpdater.Synchronous = true;//Auto-Update Settings
            AutoUpdater.Mandatory = true;
            AutoUpdater.UpdateMode = Mode.Forced;
            AutoUpdater.InstalledVersion = new Version("1.3.2.0");
            AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;
            AutoUpdater.ReportErrors = true;
            AutoUpdater.RunUpdateAsAdmin = false;
            AutoUpdater.Start("https://raw.githubusercontent.com/kanichiwah/jay_version_control/main/version.xml");

            await Task.Delay(-1);
        }

        public static void loadSettings()
        {
            quotes.Clear();
            blacklist.Clear();
            settings.Load("Settings.xml");
            ulong.TryParse(settings.SelectSingleNode("//settings/adminRole").InnerText, out adminId);
            memoryFile = settings.SelectSingleNode("//settings/memory").InnerText;
            reactEmote = settings.SelectSingleNode("//settings/reactEmote").InnerText;
            reactLimit = int.Parse(settings.SelectSingleNode("//settings/reactcount").InnerText);
            if(settings.SelectSingleNode("//settings/pinChannel").InnerText != ""){
                pinChannel = ulong.Parse(settings.SelectSingleNode("//settings/pinChannel").InnerText);
            }
            foreach (XmlNode quote in settings.SelectSingleNode("//quotes"))
            {
                quotes.Add(quote.InnerText, quote.Attributes["type"].Value);
            }
            foreach (XmlNode chanID in settings.SelectSingleNode("//blacklist"))
            {
                blacklist.Add(UInt64.Parse(chanID.InnerText));
            }
            

        }
        static void createQuote(string quoteText)
        {
            XmlNode quotes = settings.DocumentElement.SelectSingleNode("quotes");
            XmlElement nquote = settings.CreateElement("quote");
            nquote.SetAttribute("type", "normal");
            nquote.InnerText = quoteText;
            quotes.AppendChild(nquote);
            settings.Save("settings.xml");
            loadSettings();
        }
        static void deleteQuote(string quoteText)
        {
            foreach (XmlNode quoteToRemove in settings.SelectNodes("//quotes/quote"))
            {
                if (quoteToRemove.InnerText == quoteText)
                {
                    quoteToRemove.ParentNode.RemoveChild(quoteToRemove);
                    settings.Save("settings.xml");
                    loadSettings();
                    return;
                }
            }

        }
        private async Task removeReactCount(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)//if a message is unreacted
        {
            if (arg2.Id != pinChannel) //any channel other than pins
            {
                if (arg3.Emote.Name.ToString() == reactEmote)
                {
                    if (reacts.ContainsKey(arg1.Id))
                    {
                        if (!isPinned.ContainsKey(arg1.Id))
                        {
                            reacts[arg1.Id]--;
                        }
                    }
                }
            }
        }

        private void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)//check for updates
        {
            Console.WriteLine(args.Error);
            if (args != null)
            {
                if (args.IsUpdateAvailable)
                {
                    logger("update available!" +  args.CurrentVersion );
                    try
                    {
                        AutoUpdater.DownloadUpdate(args);
                        System.Environment.Exit(0);
                    }
                    catch(Exception exception)
                    {
                        logger("Error updating: " + exception);
                    }
                }
                else
                {
                    logger("No update available! " + "installed version = " + args.InstalledVersion + " latest version = " + args.CurrentVersion);
                }
            }
        }
        private async Task CountReacts(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)//if reacts are equal to x, send original message to specificed channel
        {
            if (arg2.Id != pinChannel)
            {
                await arg1.GetOrDownloadAsync(); //download message content to cache
                var pinsChannel = _client.GetChannel(pinChannel) as IMessageChannel;
                if (arg3.Emote.Name.ToString() == reactEmote)
                {
                    if (reacts.ContainsKey(arg1.Id))
                    {
                        reacts[arg1.Id]++;
                        if (reacts[arg1.Id] == reactLimit)
                        {
                            if (!isPinned.ContainsKey(arg1.Id))
                            {
                                isPinned.Add(arg1.Id, true);
                            }
                            string imageurl = "";
                            foreach (Attachment att in arg1.GetOrDownloadAsync().Result.Attachments)
                            {
                                    imageurl = att.ProxyUrl;
                            }
                            foreach(Embed em in arg1.GetOrDownloadAsync().Result.Embeds)
                            {
                                if (em.Url.Contains("tenor"))
                                {
                                    imageurl = TenorResolver.Main.resolve(em.Url);
                                }
                            }
                            var embed = new EmbedBuilder
                            {
                                Title = arg1.GetOrDownloadAsync().Result.Author.Username.ToString() + " in #" + arg1.GetOrDownloadAsync().Result.Channel.Name,
                                Color = Color.Green,
                                Description = "[Go To Message](" + arg1.GetOrDownloadAsync().Result.GetJumpUrl() + ") \r\n \r\n" + arg1.GetOrDownloadAsync().Result.Content,
                                ThumbnailUrl = arg1.GetOrDownloadAsync().Result.Author.GetAvatarUrl(),
                                Timestamp = arg1.GetOrDownloadAsync().Result.Timestamp,
                                ImageUrl = imageurl
                            };
                            await pinsChannel.SendMessageAsync(embed: embed.Build());
                        }
                    }
                    else
                    {
                        reacts.Add(arg1.Id, 1);
                    }
                }
            }
        }

        private async Task MessageReceived(SocketMessage message)
        {

            var user = message.Author as SocketGuildUser;
            var role = (user as IGuildUser).Guild.Roles.FirstOrDefault(x => x.Id == adminId);
            bool isAdmin = false;
            if (user.Roles.Contains(role))
            {
                isAdmin = true;
            }
            TrainMessage(message);
            if (message.Content.Contains("<@!813046112683950121>") || message.Content.Contains("<@813046112683950121>")){ //if bot is pinged
                if (message.Content.ToLower().Contains("rules"))
                {
                    await message.Channel.SendMessageAsync(rules);
                    return;
                }

                KeyValuePair<string,string> quoteToUse = quotes.ElementAt(rng.Next(0, quotes.Count));
                if (quoteToUse.Value == "image")
                {
                    await message.Channel.SendFileAsync(quoteToUse.Key);
                }else if(quoteToUse.Value == "username")
                {
                    string output = string.Format(quoteToUse.Value, message.Author.Username);
                    await message.Channel.SendMessageAsync(output);
                }else if (quoteToUse.Value == "markov")
                {
                    string toSend = Markov.generate().Replace("\r", "").Replace("\n", "").Replace("\u0002", "");
                    await message.Channel.SendMessageAsync(toSend);
                }
                else
                {
                    await message.Channel.SendMessageAsync(quoteToUse.Key);
                }
                return;
                 
            } 
            if (message.Content.StartsWith("!") &! message.Author.IsBot)
            {
                if (!isAdmin)
                {
                    return;
                }
                if (message.Content.Contains("!blacklist"))
                {
                    if (blacklist.Contains(message.Channel.Id))
                    {
                        await message.Channel.SendMessageAsync("This channel is already blacklisted ");
                        return;
                    }
                    blacklist.Add(message.Channel.Id);
                    XmlNode xblacklist = settings.DocumentElement.SelectSingleNode("//blacklist");
                    XmlNode channel = settings.CreateElement("channel");
                    channel.InnerText = message.Channel.Id.ToString();
                    xblacklist.AppendChild(channel);
                    settings.Save("settings.xml");
                    await message.Channel.SendMessageAsync("Blacklisted: " + message.Channel.Name);
                    loadSettings();
                }
                if (message.Content.Contains("!pinchannel"))
                {
                    pinChannel = message.Channel.Id;
                    XmlNode xpinchan = settings.DocumentElement.SelectSingleNode("//settings/pinChannel");
                    xpinchan.InnerText = message.Channel.Id.ToString();
                    settings.Save("settings.xml");
                    await message.Channel.SendMessageAsync("Pins channel set to " + message.Channel.Name);
                    loadSettings();
                }
                if (message.Content.Contains("!reactlimit"))
                {
                    string limit = "";
                    if (message.Content.Length == 11)
                    {
                        XmlNode xreactlimit = settings.DocumentElement.SelectSingleNode("//settings/reactcount");
                        await message.Channel.SendMessageAsync("React limit is currently " + xreactlimit.InnerText);
                        return;
                    }
                    else
                    {
                         limit = message.Content.Substring(12, message.Content.Length-12);
                    }
                    int limval;
                    if (int.TryParse(limit, out limval))
                    {
                        if(limval < 100)
                        {
                            XmlNode xreactlimit = settings.DocumentElement.SelectSingleNode("//settings/reactcount");
                            xreactlimit.InnerText = limit;
                            settings.Save("settings.xml");
                            await message.Channel.SendMessageAsync("React limit set to " + limit);
                            int.TryParse(limit, out reactLimit);    
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync("React limit cannot be higher than 99");
                        }
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync("Thats not a number!");
                    }
                }
                if (message.Content.Contains("!reactemote"))
                {
                    if(message.Content == "!reactemote")
                    {
                        await message.Channel.SendMessageAsync("React emote is currently: " + reactEmote);
                        return;
                    }
                    string emoteSet = message.Content.Substring(12, message.Content.Length - 12);
                    string finEmote = emoteSet.Replace("<", "").Replace(">", "").Replace(":", "");
                    string emoteName = finEmote.Substring(0, finEmote.Length - 18);
                    XmlNode xreactlimit = settings.DocumentElement.SelectSingleNode("//settings/reactEmote");
                    xreactlimit.InnerText = emoteName;
                    reactEmote = emoteName;
                    settings.Save("settings.xml");
                    await message.Channel.SendMessageAsync("React emote set to: " + emoteSet);
                }
                if (message.Content.Contains("!setrole"))
                {
                    string roleSet = message.Content.Substring(9, message.Content.Length-9);
                    string finRole = roleSet.Replace("<", "").Replace(">", "").Replace("@", "").Replace("&", "");
                    XmlNode xreactlimit = settings.DocumentElement.SelectSingleNode("//settings/adminRole");
                    xreactlimit.InnerText = finRole;
                    settings.Save("settings.xml");
                    ulong.TryParse(finRole, out adminId);
                    await message.Channel.SendMessageAsync("Admin role set to: " +  roleSet);
                }
                if (message.Content.Contains("!ac"))
                {
                    await message.Channel.SendMessageAsync(isAdmin.ToString());
                }
                if (message.Content.Contains("!add quote"))
                {
                    createQuote(message.Content.Substring(11, message.Content.Length - 11));
                    await message.Channel.SendMessageAsync("Quote added!");
                }
                if (message.Content.Contains("!remove quote"))
                {
                    deleteQuote(message.Content.Substring(13, message.Content.Length - 13));
                    await message.Channel.SendMessageAsync("Quote removed!");
                }
            }

            Console.Title = "MessageCount: " + messageCount + " fires at: " + fireAt;
            if (!blacklist.Contains(message.Channel.Id))
            {
                messageCount++;
                if (messageCount >= fireAt)
                {
                    string toSend = Markov.generate().Replace("\r", "").Replace("\n", "").Replace("\u0002", "");
                    await message.Channel.SendMessageAsync(toSend);
                    messageCount = 0;
                    rngNumber();
                }
            }

            
            
        }

        public void rngNumber()
        {
            fireAt = rng.Next(20, 75);
        }

        private void TrainMessage(SocketMessage message) //Train markov on message received
        {
            string msg = "\u0002";
            msg += message.Content + "\u0003 ";
            logger(msg);
            int averagelength = 0;
            string[] words = message.Content.Split(' ');
            foreach(string word in words)
            {
                averagelength += word.Length;
            }
            averagelength = averagelength / words.Count();
            if(message.Content.StartsWith("!") || message.Content.StartsWith("&") || message.Content.StartsWith("$") || message.Content.Contains('@'))
            {
                return;
            }
            if(message.Author.Id == _client.CurrentUser.Id) //none from ourself please!
            {
                logger("from the bot, ignore");
                return;
            }
            if(message.Content == previousmessage) //ignore spambo jambo
            {
                logger("spam");
                return;
            }
            if(words.Count() == 1) //no single word sentences
            {
                return;
            }
            if (3 >= averagelength) //sentences of average word length less than 3 get BOOTED
            {
                logger("avg less than 3 " +averagelength.ToString() );
                return;
            }
            if (msg.Contains("http")) //no links in here, no sireeeeeee
            {
                logger("link");
                return;
            }
            if (blacklist.Contains(message.Channel.Id))//eventually we will just have a check for blacklisted channels
            {
                return;
            }
            msg.Replace("\r", "").Replace("\n", "").Replace(".", "\u0003");//remove new lines and fullstops
            previousmessage = message.Content;
            Markov.markovTrain(msg);
            FileStream stream = new FileStream(memoryFile, FileMode.Append);
            using (StreamWriter writer = new StreamWriter(stream, Encoding.ASCII))
            {
                writer.WriteLine(msg);
                logger("wrote message: " + msg);
                writer.Dispose();
            }

        }

       

        public static void logger(string message)
        {
            Console.WriteLine("Log: " + message);
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
