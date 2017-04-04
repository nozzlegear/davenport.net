using Newtonsoft.Json;

namespace Davenport.Entities
{
    /// <summary>
    /// Response returned by CouchDB on POST, PUT or COPY requests.
    /// </summary>
    public class PostPutCopyResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("rev")]
        public string Rev { get; set; }

        [JsonProperty("ok")]
        public bool? Ok { get; set; }
    }
}
