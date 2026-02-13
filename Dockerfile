FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /App

# Copy everything
COPY Source Source

# Restore as distinct layers
RUN dotnet publish Source/Server/GameServer.csproj -p:LangVersion=preview -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:WarningLevel=0

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /App

COPY --from=build-env /App/Source/Server/bin/Release/net8.0/linux-x64/publish/GameServer /App/Server/GameServer

WORKDIR /Data

VOLUME /Data

EXPOSE 25555/tcp

ENTRYPOINT ["/App/Server/GameServer"]
