ARG BUILD_CONFIGURATION=Release

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
ARG TARGETARCH
RUN apt-get update && apt-get install -y \
    curl
USER $APP_UID
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION
WORKDIR /src
COPY ["./NitroxDiscordBot/NitroxDiscordBot.csproj", "NitroxDiscordBot/"]
COPY ["./NitroxDiscordBot.Db/NitroxDiscordBot.Db.csproj", "NitroxDiscordBot.Db/"]
RUN dotnet restore "NitroxDiscordBot/NitroxDiscordBot.csproj" -a "$TARGETARCH"
COPY . .
WORKDIR "/src/NitroxDiscordBot"
RUN dotnet build "NitroxDiscordBot.csproj" -c $BUILD_CONFIGURATION -o /app/build -a "$TARGETARCH"

FROM build AS publish
ARG BUILD_CONFIGURATION
RUN dotnet publish "NitroxDiscordBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -a "$TARGETARCH"

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --start-period=30s --interval=30s \
    CMD curl --fail http://localhost:5000/health || exit 1
ENTRYPOINT ["dotnet", "NitroxDiscordBot.dll"]