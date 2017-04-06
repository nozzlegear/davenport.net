using System.Collections.Generic;
using Davenport.Infrastructure;
using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class FindOptions : Serializable
    {
        [JsonProperty("fields")]
        public IEnumerable<string> Fields { get; set; }
        
        [JsonProperty("sort")]
        public IEnumerable<object> Sort { get; set; }
        
        [JsonProperty("limit")]
        public int? Limit { get; set; }
        
        [JsonProperty("skip")]
        public int? Skip { get; set; }
        
        [JsonProperty("use_index")]
        public object UseIndex { get; set; }

        [JsonProperty("selector")]
        internal string Selector { get; set; }
    }
}