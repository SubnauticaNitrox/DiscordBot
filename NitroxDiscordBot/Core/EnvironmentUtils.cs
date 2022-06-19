using System.Reflection;

namespace NitroxDiscordBot.Core;

public static class EnvironmentUtils
{
    public const string ProjectName = "Nitrox Discord Bot";
    public static string ExecutableDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
}