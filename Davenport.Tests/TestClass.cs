using Davenport.Entities;

namespace Davenport.Tests
{
    class MyTestClass : CouchDoc
    {
        public string Foo { get; set; }

        public bool Bar { get; set; }

        public int Baz { get; set; }

        public int? Bat { get; set; }
    }
}