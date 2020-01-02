# DustyBot

An open-source bot for music fan Discords.

**Webpage:** [dustybot.info](http://dustybot.info).

## Setting Up (without Visual Studio)

1. Download [dotnet core 3.1](https://dotnet.microsoft.com/download)
2. Clone the repository `git clone https://github.com/yebafan/DustyBot`
3. run `dotnet build ./DustyBot.sln` at project root
4. `cd src/DustyBot`
5. Create a new instance with `dotnet run instance create [your_instance_name] [db_encryption_pass] --token [your_bot_token] --owners [discord_id]`
   1. To enable some third-party reliant functionality you might need to specify API keys for those services. Use `dotnet run instance --help` to see all options.
   2. To add or modify API keys with an already existing instance, use `dotnet run instance modify`.
6. 🎉 Run the bot with `dotnet run run [your_instance_name] [db_encryption_pass]`
