namespace NitroxDiscordBot.Core;

internal static class EnvironmentManager
{
    private const string DotnetEnvironmentVarName = "DOTNET_ENVIRONMENT";

    public static string SetAndGetDotnetEnvironmentByBuildConfiguration()
    {
        if (Environment.GetEnvironmentVariable(DotnetEnvironmentVarName) is { } env && !string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

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

    public static string DotnetEnvironment => SetAndGetDotnetEnvironmentByBuildConfiguration();
}