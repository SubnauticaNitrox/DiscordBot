using System.ComponentModel.DataAnnotations;

namespace NitroxDiscordBot.Configuration;

public class AutoResponseConfig
{
    public IEnumerable<Definition> AutoResponseDefinitions { get; set; }

    public record Definition
    {
        [Required] public string Name { get; init; }

        public Filter[] Filters { get; init; } = [];
        public Response[] Responses { get; init; } = [];
    }

    public record Filter
    {
        public enum Types
        {
            Channel,
            UserJoinTimeSpan,
            MessageWordOrder
        }

        [Required] public Types Type { get; init; }

        public object Value { get; init; }

        [ConfigurationKeyName(nameof(Value))] public object[] Values { get; init; }

        public override string ToString() => $$"""[{{nameof(Filter)}} { {{nameof(Type)}} = {{Type}}, {{nameof(Value)}} = {{(Values != null ? string.Join(", ", Values ?? []) : Value)}} }]""";
    }

    public record Response
    {
        public enum Types
        {
            MessageRoles,
            MessageUsers
        }

        [Required] public Types Type { get; init; }

        [Required] public string Value { get; init; }
    }
}