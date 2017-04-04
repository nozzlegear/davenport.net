using Newtonsoft.Json;

namespace Davenport.Interfaces
{
    public interface ICouchDoc
    {
        /// <summary>
        /// The object's database id.
        /// </summary>
        [JsonProperty("_id")]
        string Id { get; set; }
            
        /// <summary>
        /// The object's database revision.
        /// </summary>
        [JsonProperty("_rev")]
        string Rev { get; set; }
    }
}
