using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class ListedRow<T>
    {
        
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("key")]
        public string Key { get; set; }
        
        [JsonProperty("value")]
        public Revision Value { get; set; }
        
        [JsonProperty("doc")]
        public T Doc { get; set; }
    }
}