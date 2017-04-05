using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class CouchDoc
    {
        /// <summary>
        /// The object's database id.
        /// </summary>
        [JsonProperty("_id")]
        public string Id { get; set; }
            
        /// <summary>
        /// The object's database revision.
        /// </summary>
        [JsonProperty("_rev")]
        public string Rev { get; set; }
    }
}
