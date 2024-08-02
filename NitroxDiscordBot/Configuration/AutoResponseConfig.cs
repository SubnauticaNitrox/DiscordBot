using System.ComponentModel.DataAnnotations;

namespace NitroxDiscordBot.Configuration;

public class AutoResponseConfig
{
    public IEnumerable<Definition> AutoResponseDefinitions { get; set; }

    public record Definition
    {
        [Required]
        public string Name { get; init; }
        public Filter[] Filters { get; init; } = [];
        public Response[] Responses { get; init; } = [];
    }

    public record Filter
    {
        [Required]
        [RegularExpression("Channel|UserJoinTimeSpan|MessageWordOrder")]
        public string Type { get; init; }
        public object Value { get; init; }
        [ConfigurationKeyName(nameof(Value))]
        public object[] Values { get; init; }
    }

    public record Response
    {
        [Required]
        [RegularExpression("MessageRoles")]
        public string Type { get; init; }
        [Required]
        public string Value { get; init; }
    }
}