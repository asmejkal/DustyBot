FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY "Framework" "Framework"
COPY "Database" "Database"
COPY "_Shared" "_Shared"
COPY "Service" "Service"
COPY "nuget.config" .
COPY "CodeAnalysis.ruleset" .
RUN dotnet publish "Service/src/DustyBot.Service/DustyBot.Service.csproj" -c Release -o "/app/publish"

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app
COPY --from=build "/app/publish" .
ENTRYPOINT ["dotnet", "DustyBot.Service.dll"]