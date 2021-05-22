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
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace Jay_Bot
{
    class Program
    {
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        private DiscordSocketClient _client;
        private static SpotifyClient _spotClient;
        private static string spotClientID;
        private static string spotClientSecret;
        private static string refreshToken;
        private static EmbedIOAuthServer _server;
        public static int messageCount;
        public static Random rng = new Random();
        public static string previousmessage;
        static Dictionary<ulong, int> reacts = new Dictionary<ulong, int>();
        static Dictionary<ulong, int> songReacts = new Dictionary<ulong, int>();
        static Dictionary<ulong, bool> isInPlaylist = new Dictionary<ulong, bool>();
        static Dictionary<ulong, bool> isPinned = new Dictionary<ulong, bool>();
        public static int fireAt = 20;


        //Load from xml
        public static XmlDocument settings = new XmlDocument();
        public static ulong adminId;
        public static int reactLimit;
        public static string reactEmote;
        public static string memoryFile;
        public static ulong pinChannel;
        public static ulong songChannel;
        public static int songLimit;
        public static string playlistID;
        public static string songReact;
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

            StreamReader spotifyTokenReader = new StreamReader(@"spotify.txt");
            spotClientID = spotifyTokenReader.ReadLine();
            spotClientSecret = spotifyTokenReader.ReadLine();
            spotifyTokenReader.Dispose();
            _server = new EmbedIOAuthServer(new Uri("http://localhost:5000/callback"), 5000);
            await _server.Start();

            _server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
            _server.ErrorReceived += OnErrorReceived;

            var request = new LoginRequest(_server.BaseUri, spotClientID, LoginRequest.ResponseType.Code)
            {
                Scope = new List<string> { Scopes.PlaylistModifyPublic }
            };
            BrowserUtil.Open(request.ToUri());



            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();


            _client.MessageReceived += MessageReceived; //events
            _client.ReactionAdded += CountReacts;
            _client.ReactionRemoved += removeReactCount;

            

            StreamReader sr = new StreamReader(memoryFile, Encoding.ASCII);//markov memory
            string training = sr.ReadToEnd();
            MarkovExperimental.markovTrainExperimental(training);
            Markov.markovTrain(training);
            sr.Dispose();

            rngNumber();

            AutoUpdater.Synchronous = true;//Auto-Update Settings
            AutoUpdater.Mandatory = true;
            AutoUpdater.UpdateMode = Mode.Forced;
            AutoUpdater.InstalledVersion = new Version("1.4.2.2");
            AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;
            AutoUpdater.ReportErrors = true;
            AutoUpdater.RunUpdateAsAdmin = false;
            AutoUpdater.Start("https://raw.githubusercontent.com/kanichiwah/jay_version_control/main/version.xml");

            await Task.Delay(-1);
        }

        private static async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await _server.Stop();

            var config = SpotifyClientConfig.CreateDefault();
            var tokenResponse = await new OAuthClient(config).RequestToken(
              new AuthorizationCodeTokenRequest(
                spotClientID, spotClientSecret, response.Code, new Uri("http://localhost:5000/callback")
              )
            );

             _spotClient = new SpotifyClient(tokenResponse.AccessToken);
            refreshToken = tokenResponse.RefreshToken;
        }

        private static async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await _server.Stop();
        }

        public static void loadSettings()
        {
            quotes.Clear();
            blacklist.Clear();
            settings.Load("Settings.xml");
            ulong.TryParse(settings.SelectSingleNode("//settings/adminRole").InnerText, out adminId);
            playlistID = settings.SelectSingleNode("//settings/playlistID").InnerText;
            memoryFile = settings.SelectSingleNode("//settings/memory").InnerText;
            reactEmote = settings.SelectSingleNode("//settings/reactEmote").InnerText;
            reactLimit = int.Parse(settings.SelectSingleNode("//settings/reactcount").InnerText);
            songLimit = int.Parse(settings.SelectSingleNode("//settings/songLimit").InnerText);
            songReact = settings.SelectSingleNode("//settings/songEmote").InnerText;
            if(settings.SelectSingleNode("//settings/pinChannel").InnerText != ""){
                pinChannel = ulong.Parse(settings.SelectSingleNode("//settings/pinChannel").InnerText);
            }
            if (settings.SelectSingleNode("//settings/songChannel").InnerText != "")
            {
                songChannel = ulong.Parse(settings.SelectSingleNode("//settings/songChannel").InnerText);
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
        private Task removeReactCount(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)//if a message is unreacted
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

            if (arg2.Id == songChannel) //any channel other than pins
            {
                if (arg3.Emote.Name.ToString() == songReact)
                {
                    if (songReacts.ContainsKey(arg1.Id))
                    {
                        if (!isInPlaylist.ContainsKey(arg1.Id))
                        {
                            songReacts[arg1.Id]--;
                        }
                    }
                }
            }


            return Task.CompletedTask;
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
        private async Task CountReacts(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            var message = await arg1.GetOrDownloadAsync();
            try

            {


                if (arg2.Id == songChannel)
                {
                    
                    if (!message.Content.Contains("open.spotify.com")) return;
                    if (arg3.Emote.Name.ToString() == songReact)
                    {
                        if (songReacts.ContainsKey(arg1.Id))
                        {
                            songReacts[arg1.Id]++;
                            if (songReacts[arg1.Id] == songLimit)
                            {
                                if (!isInPlaylist.ContainsKey(arg1.Id))
                                {
                                    isInPlaylist.Add(arg1.Id, true);
                                    string uri = message.Content.Replace("https://open.spotify.com/track/", "").Replace("http://open.spotify.com/track/", "").Replace("&utm_source=copy-link", "");
                                    if (uri.Contains("?si="))
                                    {
                                        int index = uri.IndexOf("?si=");
                                        uri = uri.Substring(0, index);
                                    }
                                    List<string> uris = new List<string>();
                                    uris.Add("spotify:track:" + uri);
                                    try
                                    {
                                        await _spotClient.Playlists.AddItems(playlistID, new PlaylistAddItemsRequest(uris: uris));
                                    }
                                    catch (SpotifyAPI.Web.APIUnauthorizedException ex)
                                    {
                                        var newTokenResponse = await new OAuthClient().RequestToken(new AuthorizationCodeRefreshRequest(spotClientID, spotClientSecret, refreshToken));

                                        _spotClient = new SpotifyClient(newTokenResponse.AccessToken);
                                        await _spotClient.Playlists.AddItems(playlistID, new PlaylistAddItemsRequest(uris: uris));
                                    }
                                    uris.Clear();
                                    logger("added to playlist");
                                }
                            }
                        }
                        else
                        {
                            songReacts.Add(arg1.Id, 1);
                        }

                    }
                }
            }
            catch (System.NullReferenceException ex)
            {
                await message.Channel.SendMessageAsync("Null reference exception: " +  ex.InnerException + " | " + ex.Message + " | " + ex.Source) ;
            }
            if (arg2.Id != pinChannel)
            {
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
                            foreach (Attachment att in message.Attachments)
                            {
                                    imageurl = att.ProxyUrl;
                            }
                            foreach(Embed em in message.Embeds)
                            {
                                if (em.Url.Contains("tenor"))
                                {
                                    imageurl = TenorResolver.Main.resolve(em.Url);
                                }
                            }
                            var embed = new EmbedBuilder
                            {
                                Title = message.Author.Username.ToString() + " in #" + message.Channel.Name,
                                Color = Color.Green,
                                Description = "[Go To Message](" + message.GetJumpUrl() + ") \r\n \r\n" + arg1.GetOrDownloadAsync().Result.Content,
                                ThumbnailUrl = message.Author.GetAvatarUrl(),
                                Timestamp = message.Timestamp,
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

        private string t10()
        {
            Dictionary<string, int> swordscount = new Dictionary<string, int>();
            foreach (string sword in MarkovExperimental.startWords)
            {
                if (swordscount.ContainsKey(sword)){
                    swordscount[sword]++;
                }
                else
                {
                    swordscount.Add(sword, 1);
                }
                
            }
            Dictionary<string, double> fromMark = new Dictionary<string, double>();
            foreach(var x in MarkovExperimental.dicEx.Values)
            {
                foreach(KeyValuePair<string,double> y in x)
                {
                    fromMark.Add(y.Key, y.Value);
                }
            }
            var sorted = from entry in fromMark orderby entry.Value descending select entry;
            var top10 = sorted.Take(10);
            StringBuilder sb = new StringBuilder();
            int l = 1;
            foreach (KeyValuePair<string,double> val in top10)
            {
                string clean = val.Key.ToString().Replace("\r", "").Replace("\n", "").Replace("\u0002", "");
                sb.AppendLine(l + ". " + clean + " used " + val.Value + " times to start a sentence");
                l++;
                if (l == 10) break;

            }

            return sb.ToString();
        }


        private string follows(string word)
        {
            var max = MarkovExperimental.dicEx[word].Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
            return max;
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
                    string toSend = MarkovExperimental.generateEx().Replace("\r", "").Replace("\n", "").Replace("\u0002", "");
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
                if (message.Content.Contains("!experimental"))
                {
                    string toSend = MarkovExperimental.generateEx().Replace("\r", "").Replace("\n", "").Replace("\u0002", "");
                    await message.Channel.SendMessageAsync(toSend);
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
                if(message.Content.Contains("!songchannel"))
                {
                    songChannel = message.Channel.Id;
                    XmlNode xsonchan = settings.DocumentElement.SelectSingleNode("//settings/songChannel");
                    xsonchan.InnerText = message.Channel.Id.ToString();
                    settings.Save("settings.xml");
                    await message.Channel.SendMessageAsync("Song channel set to " + message.Channel.Name);
                    loadSettings();
                }
                if (message.Content.Contains("!playlist"))
                {
                    string playID = message.Content.Substring(10);
                    if (message.Content.Length < 10)
                    {
                        await message.Channel.SendMessageAsync("No playlist ID provided");
                    }
                    XmlNode playlistnode = settings.DocumentElement.SelectSingleNode("//settings/playlistID");
                    playlistnode.InnerText = playID;
                    playlistID = playID;
                    settings.Save("settings.xml");
                    await message.Channel.SendMessageAsync("Play list ID set to " + playID);
                }
                if (message.Content.Contains("!song react"))
                {
                    if (message.Content.Length == 11)
                    {
                        await message.Channel.SendMessageAsync("Song emote is:  " + songReact);
                        return;
                    }
                    else
                    {
                        string emoteSet = message.Content.Substring(12, message.Content.Length - 12);
                        string emoteName = "";
                        if (emoteSet.Length > 15)
                        {
                            string finEmote = emoteSet.Replace("<", "").Replace(">", "").Replace(":", "");
                            emoteName = finEmote.Substring(0, finEmote.Length - 18);
                        }
                        else
                        {
                            emoteName = emoteSet;
                        }
                       
                        XmlNode xreactlimit = settings.DocumentElement.SelectSingleNode("//settings/songEmote");
                        xreactlimit.InnerText = emoteName;
                        songReact = emoteName;
                        settings.Save("settings.xml");
                        await message.Channel.SendMessageAsync("Song react emote set to: " + emoteSet);
                    }

                }
                if (message.Content.Contains("!Songreactlimit"))
                {
                    string limit = "";
                    if (message.Content.Length == 15)
                    {
                        XmlNode xreactlimit = settings.DocumentElement.SelectSingleNode("//settings/songLimit");
                        await message.Channel.SendMessageAsync("React limit is currently " + xreactlimit.InnerText);
                        return;
                    }
                    else
                    {
                        limit = message.Content.Substring(16, message.Content.Length - 16);
                    }
                    int limval;
                    if (int.TryParse(limit, out limval))
                    {
                        if (limval < 100)
                        {
                            XmlNode xreactlimit = settings.DocumentElement.SelectSingleNode("//settings/songLimit");
                            xreactlimit.InnerText = limit;
                            settings.Save("settings.xml");
                            await message.Channel.SendMessageAsync("Song react limit set to " + limit);
                            int.TryParse(limit, out songLimit);
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync("Song react limit cannot be higher than 99");
                        }
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync("Thats not a number!");
                    }
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
                    string emoteName = "";
                    if (emoteSet.Length > 15)
                    {
                        string finEmote = emoteSet.Replace("<", "").Replace(">", "").Replace(":", "");
                        emoteName = finEmote.Substring(0, finEmote.Length - 18);
                    }
                    else
                    {
                        emoteName = emoteSet;
                    }

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
                    List<string> uris = new List<string>();
                    uris.Add("spotify:track:" + "5G1sTBGbZT5o4PNRc75RKI");
                    await _spotClient.Playlists.AddItems(playlistID, new PlaylistAddItemsRequest(uris: uris));

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
                if (message.Content.Contains("!follows"))
                {
                    string word = message.Content.Substring(9);
                    await message.Channel.SendMessageAsync(follows(word));
                }
                if (message.Content.Contains("!t10"))
                {
                    await message.Channel.SendMessageAsync(t10());
                }
            }

            Console.Title = "MessageCount: " + messageCount + " fires at: " + fireAt;
            if (!blacklist.Contains(message.Channel.Id))
            {
                messageCount++;
                if (messageCount >= fireAt)
                {
                    string toSend = MarkovExperimental.generateEx().Replace("\r", "").Replace("\n", "").Replace("\u0002", "");
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
                return;
            }
            if(message.Content == previousmessage) //ignore spambo jambo
            {
                return;
            }
            if(words.Count() == 1) //no single word sentences
            {
                return;
            }
            if (3 >= averagelength) //sentences of average word length less than 3 get BOOTED
            {
                return;
            }
            if (msg.Contains("http")) //no links in here, no sireeeeeee
            {
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
