namespace Davenport.Tests
{
    /// This class specifically does not implement the CouchDoc and is instead wrapped by the CustomCouchDoc and combined with the CustomConverter.
    public class CustomDocData
    {
        public string MyId { get; set; }

        public string MyRev { get; set; }

        public bool Foo { get; set; }
    }
}