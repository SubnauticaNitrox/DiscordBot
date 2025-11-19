namespace NitroxDiscordBot.Core.Extensions;

internal static class ConfigurationBuilderExtensions
{
    extension(IConfigurationBuilder config)
    {
        public IConfigurationBuilder AddConditionalJsonFile(bool condition,
            string filePath,
            bool optional,
            bool reloadOnChange)
        {
            return !condition ? config : config.AddJsonFile(filePath, optional, reloadOnChange);
        }
    }
}