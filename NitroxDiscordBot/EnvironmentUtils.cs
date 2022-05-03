using System.Reflection;

namespace NitroxDiscordBot;

public static class EnvironmentUtils
{
    public static string ExecutableDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
}