using System.Collections.Generic;
using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class ListResponse<T>
    {
        [JsonProperty("offset")]
        public int Offset { get; set; }
        
        [JsonProperty("total_rows")]
        public int TotalRows { get; set; }
        
        [JsonProperty("rows")]
        public IEnumerable<ListedRow<T>> Rows { get; set; }

        [JsonProperty("design_docs")]
        public IEnumerable<ListedRow<object>> DesignDocs { get; set; }
    }
}