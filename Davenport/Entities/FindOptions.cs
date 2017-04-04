using System.Collections.Generic;
using Davenport.Infrastructure;
using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class FindOptions : Serializable
    {
        [JsonProperty("fields")]
        public string fields { get; set; }
        
        [JsonProperty("sort")]
        public IEnumerable<object> sort { get; set; }
        
        [JsonProperty("limit")]
        public int? limit { get; set; }
        
        [JsonProperty("skip")]
        public int? skip { get; set; }
        
        [JsonProperty("use_index")]
        public object use_index { get; set; }

        [JsonProperty("selector")]
        internal string Selector { get; set; }
    }
}