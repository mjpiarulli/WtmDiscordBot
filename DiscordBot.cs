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
        private readonly EsiClient _esiClient;
        

        private DiscordSocketClient _discordClient;
        private string _refreshToken;
        private DateTime _esiTokenExpires;
        private long _mailingListId;


        public DiscordBot(string discordAuthToken, string esiClientId, string esiSecretKey, string approvedDiscordUsers, string mailingListName)
        {
            _discordAuthToken = discordAuthToken;
            _approvedDiscordUsers = approvedDiscordUsers;
            _mailingListName = mailingListName;
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
            var scopes = new List<string>{"esi-mail.send_mail.v1", "esi-search.search_structures.v1", "esi-mail.read_mail.v1", "esi-mail.organize_mail.v1"};
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
            _mailingListId = response.Data.Where(ml => ml.Name.ToLower() == _mailingListName.ToLower()).First().MailingListId;

            // set up the discord bot
            _discordClient = new DiscordSocketClient();                        
            _discordClient.ReactionAdded += ReactionAdded;

            await _discordClient.LoginAsync(TokenType.Bot, _discordAuthToken);
            await _discordClient.StartAsync();

            await Task.Delay(-1);
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

            // check to make sure the user who placed the reaction on the message is an approved user and
            // that the reaction is one we care about
            if(_approvedDiscordUsers.IndexOf(reaction.User.ToString()) > 0 &&
                reaction.Emote.Name == "sendmail")
            {
                var discordMessage = await channel.GetMessageAsync(message.Id);
                var body = discordMessage.Embeds.FirstOrDefault().Footer.ToString();
                var subject = Regex.Match(body, @"<url.*>Kill: (.*)</url>").Groups[1].Value;
                var recipients = new[] {new{ recipient_id = _mailingListId, recipient_type = "mailing_list"}};

                var response = await _esiClient.Mail.New(recipients, subject, body);
            }                
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
            
            var token = await _esiClient.SSO.GetToken(GrantType.RefreshToken, _refreshToken);
            var authChar = await _esiClient.SSO.Verify(token);
            _refreshToken = authChar.RefreshToken;
            _esiTokenExpires = authChar.ExpiresOn;
            _esiClient.SetCharacterData(authChar);
        }

        /// <summary>
        /// pull the return sso code from the browser
        /// </summary>
        private string GetEsiAuthToken()
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
                    Console.WriteLine(ex.ToString());
                }
            }

            clientSocket.Close();
            serverSocket.Stop();

            return code;
        }
    }
}
