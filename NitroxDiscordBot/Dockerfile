FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["NitroxDiscordBot/NitroxDiscordBot.csproj", "NitroxDiscordBot/"]
RUN dotnet restore "NitroxDiscordBot/NitroxDiscordBot.csproj"
COPY . .
WORKDIR "/src/NitroxDiscordBot"
RUN dotnet build "NitroxDiscordBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NitroxDiscordBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NitroxDiscordBot.dll"]