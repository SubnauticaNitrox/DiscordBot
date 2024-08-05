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
            [Description("Any one of the following channels")]
            AnyChannel,
            [Description("Maximum newishness of a user to this server")]
            UserJoinAge,
            [Description("Sentences containing the same individual words and order")]
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