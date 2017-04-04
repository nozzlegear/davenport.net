using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class Revision
    {
        [JsonProperty("rev")]
        public string Rev { get; set; }
    }
}