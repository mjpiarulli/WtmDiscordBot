using System.Configuration;
using System.Threading.Tasks;

namespace WtmDiscordBot
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var discordToken = ConfigurationManager.AppSettings["discordBotToken"].ToString();
            var esiClientId = ConfigurationManager.AppSettings["esiClientId"].ToString();
            var esiSecretKey = ConfigurationManager.AppSettings["esiSecretKey"].ToString();
            var approvedDiscordUsers = ConfigurationManager.AppSettings["approvedDiscordUsers"].ToString();
            var mailingListName = ConfigurationManager.AppSettings["mailingListName"].ToString();
            var channelToWatch = ConfigurationManager.AppSettings["channelToWatch"].ToString();

            var discordBot = new DiscordBot(discordToken, esiClientId, esiSecretKey, approvedDiscordUsers, mailingListName, channelToWatch);
            await discordBot.Start();

            await Task.Delay(-1);
        }        
    }
}
