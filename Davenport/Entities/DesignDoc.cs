using System.Collections.Generic;
using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class DesignDoc : CouchDoc
    {
        /// <summary>
        /// Views that are part of this design doc.
        /// </summary>
        [JsonProperty("views")]
        public Dictionary<string, View> Views { get; set; }

        /// <summary>
        /// The language used by this design doc. Currently only supports 'javascript'.
        /// </summary>
        [JsonProperty("language")]
        public string Language { get; set; }
    }
}