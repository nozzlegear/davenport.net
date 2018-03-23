using System.Collections.Generic;
using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class DesignDocConfig
    {
        /// <summary>
        /// The design doc's name. Should be URL-encoded where necessary.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Views that should be part of this design doc.
        /// </summary>
        [JsonProperty("views")]
        public IEnumerable<View> Views { get; set; }
    }
}