using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class CouchResponse
    {
        /// <summary>
        /// Whether the request was successful or not.
        /// </summary>
        [JsonProperty("ok")]
        public bool Ok { get; set; }
    }
}
