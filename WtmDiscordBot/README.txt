Setup

Log in to https://developers.eveonline.com/
Manage applications
Create New Application
Application Currently needs the following scopes
	publicData
	esi-mail.organize_mail.v1
	esi-mail.read_mail.v1
	esi-mail.send_mail.v1
	esi-search.search_structures.v1

Log in to https://discord.com/developers/applications
Follow these instructions to create the resources required to create a discord bot: https://discordpy.readthedocs.io/en/latest/discord.html


WtmDiscordBot.exe.config (root folder WtmDiscordBot/bin/Debug) or App.config within application
	esiClientId is Client ID within the new application you created on develeopers.eveonline.com
	esiSecretKey is Secret Key within th enew application you created on develeopers.eveonline.com
	discordBotToken is in the Bot section of https://discord.com/developers/applications (press the click to reveal link)
	approvedDiscordUsers are the full discord user names that the bot will respond to.  This field is case sensative
	mailingListName is the name of the in game mailing list you want to send the mails to.

Once this is done double click the WtmDiscordBot.exe (root folder WtmDiscordBot/bin/Debug).  You'll be prompted to login to your eve account.  These will be the account the mails will go to.
Once you login and see the blank page with ?code= in the url, you can close the browser window and the bot is should be online in your discord channel and ready to start sending mails.  
Closing the console window will deactivate the bot.


Code Overview

Program.cs is the entry point for the app.  The "Main" method will be called first to gather the info from the App.config and then create the "DiscordBot" object

DiscordBot.cs is the object that actually uses the information stored in the App.config to do all the things! :P