using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class View
    {
        /// <summary>
        /// The view's name.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
        
        /// <summary>
        /// Required: The view's map function. Must be valid JavaScript.
        /// </summary>
        [JsonProperty("map")]
        public string MapFunction { get; set; }
        
        /// <summary>
        /// Optional: The view's reduce function. Must be valid JavaScript.
        /// </summary>
        [JsonProperty("reduce")]
        public string ReduceFunction { get; set; }
    }
}