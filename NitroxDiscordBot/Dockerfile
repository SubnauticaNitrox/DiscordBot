FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
ARG TARGETARCH
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["./NitroxDiscordBot/NitroxDiscordBot.csproj", "NitroxDiscordBot/"]
COPY ["./NitroxDiscordBot.Db/NitroxDiscordBot.Db.csproj", "NitroxDiscordBot.Db/"]
RUN dotnet restore "NitroxDiscordBot/NitroxDiscordBot.csproj" -a "$TARGETARCH"
COPY . .
WORKDIR "/src/NitroxDiscordBot"
RUN dotnet build "NitroxDiscordBot.csproj" -c $BUILD_CONFIGURATION -o /app/build -a "$TARGETARCH"

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "NitroxDiscordBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -a "$TARGETARCH"

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NitroxDiscordBot.dll"]
