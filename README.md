# DustyBot

An open-source bot for music fan Discords.

**Webpage:** [dustybot.info](http://dustybot.info).

## Setting Up

1. Download [dotnet core 3.1](https://dotnet.microsoft.com/download)
2. Clone the repository `git clone https://github.com/yebafan/DustyBot`
3. run `dotnet build ./DustyBot.sln` at project root
4. `cd src/DustyBot`
5. Create a new instance with `dotnet run instance create [your_instance_name] [db_encryption_pass] --token [your_bot_token] --owners [discord_id]`
6. 🎉 Next time you need to run the bot again use `dotnet run run [your_instance_name] [db_encryption_pass]` to reuse the previous configuration
