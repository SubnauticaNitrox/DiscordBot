namespace NitroxDiscordBot.Core;

internal static class EnvironmentManager
{
    private const string DotnetEnvironmentVarName = "DOTNET_ENVIRONMENT";

    public static string SetAndGetDotnetEnvironmentByBuildConfiguration()
    {
        if (Environment.GetEnvironmentVariable(DotnetEnvironmentVarName) is null)
        {
            string value =
#if DEBUG
            "Development"
#else
            "Production"
#endif
                ;
            Environment.SetEnvironmentVariable(DotnetEnvironmentVarName, value);
            return value;
        }
        return "Development";
    }

    public static string DotnetEnvironment => SetAndGetDotnetEnvironmentByBuildConfiguration();
}