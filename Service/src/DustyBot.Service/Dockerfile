#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["Service/src/DustyBot.Service/DustyBot.Service.csproj", "Service/src/DustyBot.Service/"]
COPY ["_Shared/src/DustyBot.LastFm/DustyBot.LastFm.csproj", "_Shared/src/DustyBot.LastFm/"]
COPY ["_Shared/src/DustyBot.Core/DustyBot.Core.csproj", "_Shared/src/DustyBot.Core/"]
COPY ["Framework/src/DustyBot.Framework/DustyBot.Framework.csproj", "Framework/src/DustyBot.Framework/"]
COPY ["Database/src/DustyBot.Database.Services/DustyBot.Database.Services.csproj", "Database/src/DustyBot.Database.Services/"]
COPY ["Database/src/DustyBot.Database.Core/DustyBot.Database.Core.csproj", "Database/src/DustyBot.Database.Core/"]
COPY ["Database/src/DustyBot.Database.TableStorage/DustyBot.Database.TableStorage.csproj", "Database/src/DustyBot.Database.TableStorage/"]
COPY ["Database/src/DustyBot.Database.Mongo/DustyBot.Database.Mongo.csproj", "Database/src/DustyBot.Database.Mongo/"]
RUN dotnet restore "Service/src/DustyBot.Service/DustyBot.Service.csproj"
COPY . .
WORKDIR "/src/Service/src/DustyBot.Service"
RUN dotnet build "DustyBot.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DustyBot.Service.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DustyBot.Service.dll"]