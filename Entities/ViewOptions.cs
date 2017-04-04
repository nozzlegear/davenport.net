using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class ViewOptions : ListOptions
    {
        [JsonProperty("reduce")]
        public bool? Reduce { get; set; }

        [JsonProperty("group")]
        public bool? Group { get; set; }

        [JsonProperty("group_level")]
        public int? GroupLevel { get; set; }
    }
}