using Discord;
using Discord.WebSocket;
using ESI.NET;
using ESI.NET.Enumerations;
using ESI.NET.Models.SSO;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WtmDiscordBot
{
    public class DiscordBot
    {
        private readonly string _discordAuthToken;
        private readonly string _approvedDiscordUsers;
        private readonly string _mailingListName;
        private readonly string _channelToWatch;

        private readonly EsiClient _esiClient;

        private DiscordSocketClient _discordClient;
        private string _refreshToken;
        private DateTime _esiTokenExpires;
        private long _mailingListId;


        public DiscordBot(string discordAuthToken, string esiClientId, string esiSecretKey, string approvedDiscordUsers, string mailingListName, string channelToWatch)
        {
            _discordAuthToken = discordAuthToken;
            _approvedDiscordUsers = approvedDiscordUsers;
            _mailingListName = mailingListName;
            _channelToWatch = channelToWatch;

            IOptions<EsiConfig> config = Options.Create(new EsiConfig()
            {
                EsiUrl = "https://esi.evetech.net/",
                DataSource = DataSource.Tranquility,
                ClientId = esiClientId,
                SecretKey = esiSecretKey,
                CallbackUrl = "http://localhost:12847",
                UserAgent = "Warp To Me Incursions"
            });

            _esiClient = new EsiClient(config);
        }

        public async Task Start()
        {
            // set up esi connection
            var scopes = new List<string> { "esi-mail.send_mail.v1", "esi-search.search_structures.v1", "esi-mail.read_mail.v1", "esi-mail.organize_mail.v1" };
            var url = _esiClient.SSO.CreateAuthenticationUrl(scopes);
            System.Diagnostics.Process.Start(url);
            var esiAuthToken = GetEsiAuthToken();
            var token = await _esiClient.SSO.GetToken(GrantType.AuthorizationCode, esiAuthToken);
            var authChar = await _esiClient.SSO.Verify(token);
            _refreshToken = authChar.RefreshToken;
            _esiTokenExpires = authChar.ExpiresOn;
            _esiClient.SetCharacterData(authChar);

            // get the mailing list id
            var response = await _esiClient.Mail.MailingLists();
            var mailingList = response.Data.Where(ml => ml.Name.ToLower() == _mailingListName.ToLower()).FirstOrDefault();
            if (mailingList == null)
                Log("The character  used does not have access to the mailing list specified in the config. Please look at mailingListName app setting.");

            _mailingListId = mailingList.MailingListId;

            // set up the discord bot
            _discordClient = new DiscordSocketClient();
            _discordClient.ReactionAdded += ReactionAdded;
            _discordClient.MessageReceived += MessageReceived;

            await _discordClient.LoginAsync(TokenType.Bot, _discordAuthToken);
            await _discordClient.StartAsync();

            await Task.Delay(-1);
        }

        public void Log(string message)
        {
            Console.WriteLine($"{DateTime.Now:MM/dd/yyyy HH:mm:ss}: {message}");
        }


        public async Task MessageReceived(SocketMessage message)
        {
            // check if the esi token needs to be refreshed and refresh it
            await RefreshEsiToken();

            if (!message.Channel.Name.Equals(_channelToWatch, StringComparison.OrdinalIgnoreCase))
            {
                Log("Message received in channel bot is not watching.  Ignoring message");
                return;
            }

            await BuildAndSendMessage(message);
        }

        /// <summary>
        /// The event method that gets called whenever a reaction is placed on a message
        /// </summary>
        /// <param name="message">the message the reaction was added to</param>
        /// <param name="channel">the discord channel the message came from</param>
        /// <param name="reaction">the reaction on the message from the channel</param>
        /// <returns></returns>
        public async Task ReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            // check if the esi token needs to be refreshed and refresh it
            await RefreshEsiToken();

            if (!channel.Name.Equals(_channelToWatch, StringComparison.OrdinalIgnoreCase))
            {
                Log("Reaction received in channel bot is not watching.  Ignoring message");
                return;
            }

            // check to make sure the user who placed the reaction on the message is an approved user and
            // that the reaction is one we care about
            if (!_approvedDiscordUsers.Contains(reaction.User.ToString()))
            {
                Log($"Unapproved user added reaction to message: {reaction.User}");
                return;
            }
            if (!reaction.Emote.Name.Equals("sendmail", StringComparison.OrdinalIgnoreCase))
            {
                Log($"Other emote detected on message: {reaction.Emote.Name}");
                return;
            }

            var discordMessage = await channel.GetMessageAsync(message.Id);
            if (discordMessage == null)
            {
                Log($"Unable to find discord message from channel {channel.Name}");
                return;
            }

            await BuildAndSendMessage(discordMessage);

        }

        private async Task BuildAndSendMessage(IMessage discordMessage)
        {
            var embed = discordMessage.Embeds.FirstOrDefault();
            if (embed == null)
            {
                Log($"Message doesn't have an embed section.  Ignoring");
                return;
            }

            var body = embed.Footer.ToString();

            var regExMatch = Regex.Match(body, @"<url.*>Kill: (.*)</url>");

            if (!regExMatch.Success)
            {
                Log("Unable to parse subject line out of embed");
                return;
            }

            var subject = regExMatch.Groups[1].Value;
            var recipients = new[] { new { recipient_id = _mailingListId, recipient_type = "mailing_list" } };

            var response = await _esiClient.Mail.New(recipients, subject, body);

            if (response.Exception != null)
                Log(response.Exception.Message);
        }


        /// <summary>
        /// the token esi provides is only good for a certain amount of time so here we check
        /// to see if the token has expired and if it has refresh it
        /// </summary>
        /// <returns></returns>
        private async Task RefreshEsiToken()
        {
            if (DateTime.Now < _esiTokenExpires)
                return;

            try
            {
                var token = await _esiClient.SSO.GetToken(GrantType.RefreshToken, _refreshToken);
                var authChar = await _esiClient.SSO.Verify(token);
                _refreshToken = authChar.RefreshToken;
                _esiTokenExpires = authChar.ExpiresOn;
                _esiClient.SetCharacterData(authChar);

                Log("Refreshed ESI token");
            }
            catch (Exception e)
            {
                Log("Error occured while refreshing ESI token");
                Log(e.Message);
            }
        }

        /// <summary>
        /// pull the return sso code from the browser
        /// </summary>
        private string GetEsiAuthToken()
        {
            try
            {
                var ipAddress = IPAddress.Parse("127.0.0.1");
                var serverSocket = new TcpListener(ipAddress, 12847);
                int requestCount = 0;
                var clientSocket = default(TcpClient);
                serverSocket.Start();
                clientSocket = serverSocket.AcceptTcpClient();
                requestCount = 0;
                var code = string.Empty;

                while (true)
                {
                    try
                    {
                        requestCount = requestCount + 1;
                        NetworkStream networkStream = clientSocket.GetStream();
                        byte[] bytesFrom = new byte[1002500];
                        networkStream.Read(bytesFrom, 0, clientSocket.ReceiveBufferSize);
                        string dataFromClient = Encoding.ASCII.GetString(bytesFrom);
                        var match = Regex.Match(dataFromClient, @"\/\?code=([a-zA-Z0-9-_]*)");
                        code = match.Groups[1].Value;
                        string serverResponse = @"HTTP/1.1 200 OK
                                                Date: Mon, 27 Jul 2009 12:28:53 GMT
                                                Server: Apache/2.2.14 (Win32)
                                                Last-Modified: Wed, 22 Jul 2009 19:15:56 GMT
                                                Content-Length: 88
                                                Content-Type: text/html
                                                Connection: Closed
                                                <html>
                                                <body>
                                                <h1>Hi! You can close this page now.</h1>
                                                </body>
                                                </html>";
                        byte[] sendBytes = Encoding.ASCII.GetBytes(serverResponse);
                        networkStream.Write(sendBytes, 0, sendBytes.Length);
                        networkStream.Flush();
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log("Error occured with the stream from the browser");
                        Log(ex.Message);
                    }
                }

                clientSocket.Close();
                serverSocket.Stop();

                return code;
            }
            catch (Exception e)
            {
                Log("Error occured while getting the ESI code from the browser");
                Log(e.Message);
                return string.Empty;
            }
        }
    }
}
