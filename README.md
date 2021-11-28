# DustyBot

An open-source bot for music fan Discords.

**Webpage:** [dustybot.info](http://dustybot.info).

## Running the bot
1. Install docker
2. Clone the project and go to the root folder
3. Create an `.env` file and copy in the environment variables listed below (or set them in your shell)
4. `docker-compose build`
5. `docker-compose up`
6. :tada:

## Variables
```ini
# Required for the bot to run:
Dusty_DefaultCommandPrefix={A command prefix for the bot}
Dusty_OwnerID={Your Discord ID}
Dusty_BotToken={Your bot account token}
Dusty_MongoRootUsername=superuser
Dusty_MongoRootPassword={Insert any random strong password here}
Dusty_WebsiteRoot=https://dustybot.info
Dusty_WebsiteShorthand=dustybot.info

# Required only for some modules
Dusty_YouTubeKey=
Dusty_LastFmKey=
Dusty_SpotifyId=
Dusty_SpotifyKey=
Dusty_TableStorageConnectionString=
Dusty_PapagoClientId=
Dusty_PapagoClientSecret=
Dusty_PolrKey=
Dusty_PolrDomain=
Dusty_SupportServerInvite=
Dusty_SpotifyConnectUrl=
Dusty_PatreonUrl=
```

## Debugging in Visual Studio
1. Clone the project and go to the root folder
2. Create an `.env` file and copy in the environment variables listed above
3. Open the solution and run the `docker-compose` project
4. :tada:
