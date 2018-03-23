using Davenport.Entities;

namespace Davenport.Tests
{
    /// Wraps the CustomDocData class and is combined with a custom json converter to map properties to a couchdoc.
    public class CustomDoc : CouchDoc
    {
        public CustomDoc(CustomDocData data)
        {
            Data = data;
        }

        public CustomDocData Data { get; set; }
    }
}