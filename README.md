# DustyBot

An open-source bot for music fan Discords.

**Webpage:** [dustybot.info](http://dustybot.info).

## Setting Up (without Visual Studio)

1. Setup a MongoDB database (https://www.mongodb.com/)
2. Download [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download) for your OS
3. Clone the repository `git clone https://github.com/yebafan/DustyBot`
5. `cd src/DustyBot`
4. Run `dotnet build`
6. Configure your instance with `dotnet run configure [mongo_connection_string] --token [your_bot_token] --owners [discord_id]`
   1. To enable some third-party reliant functionality you might need to specify API keys for those services. Use `dotnet run configure --help` to see all options.
6. 🎉 Run the bot with `dotnet run run [mongo_connection_string]`
