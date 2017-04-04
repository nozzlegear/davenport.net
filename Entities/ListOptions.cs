using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Flurl;
using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class ListOptions
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

        public IEnumerable<QueryParameter> ToQueryParameters()
        {
            var output = new List<QueryParameter>();

            foreach (PropertyInfo property in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = property.GetValue(this, null);
                string propName = property.Name;

                if (value == null)
                {
                    continue;
                }

                if (property.CustomAttributes.Any(att => att.AttributeType == typeof(JsonPropertyAttribute)))
                {
                    // Use the JsonPropertyName instead of the C# property namespace
                    var att = property.GetCustomAttributes(typeof(JsonPropertyAttribute), false).Cast<JsonPropertyAttribute>().FirstOrDefault();

                    propName = att?.PropertyName ?? property.Name;
                }

                QueryParameter param;

                if (propName == "keys" || propName == "key" || propName == "start_key" || propName == "end_key")
                {
                    // Keys must be JSON serialized
                    param = new QueryParameter(propName, JsonConvert.SerializeObject(value));
                }
                else
                {
                    param = new QueryParameter(propName, value);
                }

                output.Add(param);
            }

            return output;
        }
    }
}