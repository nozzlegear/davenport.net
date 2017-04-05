using Newtonsoft.Json;

namespace Davenport.Entities
{
    /// <summary>
    /// Response returned by CouchDB on POST, PUT or COPY requests.
    /// </summary>
    public class PostPutCopyResponse : Revision
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("ok")]
        public bool? Ok { get; set; }
    }
}
