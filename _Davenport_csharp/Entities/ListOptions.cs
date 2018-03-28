using System.Collections.Generic;
using System.Linq;
using Davenport.Infrastructure;
using Flurl;
using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class ListOptions : Serializable
    {
        [JsonProperty("limit")]
        public int? Limit { get; set; }
        
        [JsonProperty("key")]
        public string Key { get; set; }
        
        [JsonProperty("keys")]
        public IEnumerable<string> Keys { get; set; }
        
        [JsonProperty("start_key")]
        public object StartKey { get; set; }
        
        [JsonProperty("end_key")]
        public object EndKey { get; set; }
        
        [JsonProperty("inclusive_end")]
        public bool? InclusiveEnd { get; set; }
        
        [JsonProperty("descending")]
        public bool? Descending { get; set; }
        
        [JsonProperty("skip")]
        public int? Skip { get; set; }

        public override IEnumerable<QueryParameter> ToQueryParameters()
        {
            var kvps = ToDictionary();
            string[] keys = { "keys", "key", "start_key", "end_key" };
            var matched = kvps.Where(kvp => keys.Any(key => key == kvp.Key));
            var allElse = kvps.Where(kvp => ! matched.Contains(kvp)).ToList();

            foreach (var kvp in matched)
            {
                // These keys must be JSON serialized
                allElse.Add(new KeyValuePair<string, object>(kvp.Key, JsonConvert.SerializeObject(kvp.Value)));
            }

            return allElse.Select(kvp => new QueryParameter(kvp.Key, kvp.Value));
        }
    }
}