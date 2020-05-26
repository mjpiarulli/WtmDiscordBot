using System;
using System.Configuration;
using System.Threading.Tasks;

namespace WtmDiscordBot
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                var discordToken = ConfigurationManager.AppSettings["discordBotToken"].ToString();
                var esiClientId = ConfigurationManager.AppSettings["esiClientId"].ToString();
                var esiSecretKey = ConfigurationManager.AppSettings["esiSecretKey"].ToString();
                var approvedDiscordUsers = ConfigurationManager.AppSettings["approvedDiscordUsers"].ToString();
                var mailingListName = ConfigurationManager.AppSettings["mailingListName"].ToString();
                var channelToWatch = ConfigurationManager.AppSettings["channelToWatch"].ToString();

                var discordBot = new DiscordBot(discordToken, esiClientId, esiSecretKey, approvedDiscordUsers, mailingListName, channelToWatch);
                await discordBot.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("An unexpected error occurred.  Please check messages above.");
            }

            await Task.Delay(-1);
        }
    }
}
