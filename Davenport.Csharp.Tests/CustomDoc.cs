using Davenport.Csharp.Types;

namespace Davenport.Tests
{
    /// Wraps the CustomDocData class and is combined with a custom json converter to map properties to a couchdoc.
    public class CustomDoc : CouchDoc
    {
        public CustomDoc(CustomDocData data)
        {
            Data = data;
        }

        public override string Id { get; set; }

        public override string Rev { get; set; }

        public CustomDocData Data { get; set; }
    }
}