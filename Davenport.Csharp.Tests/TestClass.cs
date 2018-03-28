using Davenport.Csharp.Types;

namespace Davenport.Tests
{
    class MyTestClass : CouchDoc
    {
        public override string Id { get; set; }

        public override string Rev { get; set; }

        public string Foo { get; set; }

        public bool Bar { get; set; }

        public int Baz { get; set; }

        public int? Bat { get; set; }
    }
}