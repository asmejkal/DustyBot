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
Dusty_Discord_Token={Your bot account token}
Dusty_Mongo_RootUsername=superuser
Dusty_Mongo_RootPassword={Insert any random strong password here}
Dusty_Web_WebsiteRoot=https://dustybot.info
Dusty_Web_WebsiteShorthand=dustybot.info

# Required only for some modules
Dusty_YouTube_ApiKey=
Dusty_LastFm_ApiKey=
Dusty_Spotify_ClientId=
Dusty_Spotify_ClientSecret=
Dusty_TableStorage_ConnectionString=
Dusty_Papago_ClientId=
Dusty_Papago_ClientSecret=
Dusty_Polr_ApiKey=
Dusty_Polr_Domain=
Dusty_Web_SupportServerInvite=
Dusty_Web_SpotifyConnectUrl=
Dusty_Web_PatreonUrl=
```

## Debugging in Visual Studio
1. Clone the project and go to the root folder
2. Create an `.env` file and copy in the environment variables listed above
3. Open the solution and run the `docker-compose` project
4. :tada:

## Debugging without Docker
1. Run and configure a Mongo DB instance, either in Docker or locally
2. Configure required environment variables in the `DustyBot.Service` project properties 
    - See `dustybot-service-template.yml` for the list of variables
3. Run the `DustyBot.Service` project
