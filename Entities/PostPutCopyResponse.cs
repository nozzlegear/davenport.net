namespace Davenport.Entities
{
    /// <summary>
    /// Response returned by CouchDB on POST, PUT or COPY requests.
    /// </summary>
    public class PostPutCopyResponse
    {
        public string id { get; set; }

        public string rev { get; set; }

        public bool? ok { get; set; }
    }
}
