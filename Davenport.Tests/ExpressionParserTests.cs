using System;
using Davenport.Infrastructure;
using Xunit;

namespace Davenport.Tests
{
    public class ExpressionParserTests
    {
        [Fact(DisplayName = "")]
        public void StringEquals()
        {
            var parser = new ExpressionParser();
			var myClass = new MyTestClass()
			{
				Foo = "test",
				Baz = 5,
			};
			var t = new
			{
				Baz = 5
			};
			var search = 5;
			
			//c.Find(x => x.Foo == searchString);
			parser.Find<MyTestClass>(x => x.Bat > myClass.Baz && x.Bat < myClass.Baz && x.Bat == myClass.Baz);
        }
    }

    class MyTestClass
	{
		public string Id { get; set; }
		
		public string Foo { get; set; }
		
		public bool Bar { get; set; }
		
		public int Baz { get; set; }

		public int? Bat { get; set; }
	}
}
