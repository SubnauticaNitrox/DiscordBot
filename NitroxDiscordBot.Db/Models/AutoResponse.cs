using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NitroxDiscordBot.Db.Models;

[Table("AutoResponses")]
public record AutoResponse
{
    public int AutoResponseId { get; set; }
    [Required]
    [MinLength(3)]
    [MaxLength(255)]
    public string Name { get; set; } = "";
    public ICollection<Filter> Filters { get; set; } = [];
    public ICollection<Response> Responses { get; set; } = [];

    [Table("AutoResponseFilters")]
    public record Filter
    {
        public enum Types
        {
            [Description("Filter on channel id")]
            Channel,
            [Description("Filter on user newishness")]
            UserJoinTimeSpan,
            [Description("Filter on word order in sentences written in a message")]
            MessageWordOrder
        }

        public int FilterId { get; set; }
        [Required]
        public Types Type { get; set; }
        [Required]
        public string[] Value { get; set; } = [];
    }

    [Table("AutoResponseResponses")]
    public record Response
    {
        public enum Types
        {
            MessageRoles,
            MessageUsers
        }

        public int ResponseId { get; set; }
        [Required]
        public Types Type { get; set; }
        [Required]
        public string[] Value { get; set; } = [];
    }
}