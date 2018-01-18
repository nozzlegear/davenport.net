using System.Collections.Generic;
using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class FindResult<DocumentType> where DocumentType : CouchDoc
    {
        [JsonProperty("warning")]
        public string Warning { get; set; }

        [JsonProperty("docs")]
        public IEnumerable<DocumentType> Docs { get; set; }
    }
}