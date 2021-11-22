#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["Service/src/DustyBot.Service.New/DustyBot.Service.New.csproj", "Service/src/DustyBot.Service.New/"]
COPY ["../Disqord/src/Disqord.Bot/Disqord.Bot.csproj", "../Disqord/src/Disqord.Bot/"]
COPY ["../Disqord/src/Disqord/Disqord.csproj", "../Disqord/src/Disqord/"]
COPY ["../Disqord/src/Disqord.Gateway/Disqord.Gateway.csproj", "../Disqord/src/Disqord.Gateway/"]
COPY ["../Disqord/src/Disqord.Gateway.Api/Disqord.Gateway.Api.csproj", "../Disqord/src/Disqord.Gateway.Api/"]
COPY ["../Disqord/src/Disqord.Core/Disqord.Core.csproj", "../Disqord/src/Disqord.Core/"]
COPY ["../Disqord/src/Disqord.Webhook/Disqord.Webhook.csproj", "../Disqord/src/Disqord.Webhook/"]
COPY ["../Disqord/src/Disqord.Rest/Disqord.Rest.csproj", "../Disqord/src/Disqord.Rest/"]
COPY ["../Disqord/src/Disqord.Rest.Api/Disqord.Rest.Api.csproj", "../Disqord/src/Disqord.Rest.Api/"]
COPY ["_Shared/src/DustyBot.LastFm/DustyBot.LastFm.csproj", "_Shared/src/DustyBot.LastFm/"]
COPY ["Framework/src/DustyBot.Framework.New/DustyBot.Framework.New.csproj", "Framework/src/DustyBot.Framework.New/"]
COPY ["_Shared/src/DustyBot.Core/DustyBot.Core.csproj", "_Shared/src/DustyBot.Core/"]
COPY ["Database/src/DustyBot.Database.Services/DustyBot.Database.Services.csproj", "Database/src/DustyBot.Database.Services/"]
COPY ["Database/src/DustyBot.Database.Core/DustyBot.Database.Core.csproj", "Database/src/DustyBot.Database.Core/"]
COPY ["Database/src/DustyBot.Database.TableStorage/DustyBot.Database.TableStorage.csproj", "Database/src/DustyBot.Database.TableStorage/"]
COPY ["Database/src/DustyBot.Database.Mongo/DustyBot.Database.Mongo.csproj", "Database/src/DustyBot.Database.Mongo/"]
RUN dotnet restore "Service/src/DustyBot.Service.New/DustyBot.Service.New.csproj"
COPY . .
WORKDIR "/src/Service/src/DustyBot.Service.New"
RUN dotnet build "DustyBot.Service.New.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DustyBot.Service.New.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DustyBot.Service.New.dll"]