using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class ViewResult<T>
    {
        [JsonProperty("key")]
        public string Key { get; set; }
        
        [JsonProperty("value")]
        public T Value { get; set; }
    }
}