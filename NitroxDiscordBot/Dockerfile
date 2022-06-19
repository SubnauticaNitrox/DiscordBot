FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
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
