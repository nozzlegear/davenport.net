using System;
using Davenport.Infrastructure;
using Xunit;

namespace Davenport.Tests
{
    public class ExpressionParserTests
    {
        ExpressionParser Parser = new ExpressionParser();

        MyTestClass TestClass = new MyTestClass()
        {
            Foo = "test",
            Bar = false,
            Baz = 5,
            Bat = null,
        };

        [Fact(DisplayName = "Parser FromClass")]
        public void FromClass()
        { 	
			string result = Parser.Parse<MyTestClass>(x => x.Baz == TestClass.Baz);

            Assert.True(result == $"\"Baz\": {{ \"$eq\": {TestClass.Baz} }}");
        }

        [Fact(DisplayName = "Parser FromVariable")]
        public void FromVariable()
        {
            string searchString = "test";
            string result = Parser.Parse<MyTestClass>(x => x.Foo == searchString);

            Assert.True(result == $"\"Foo\": {{ \"$eq\": \"{searchString}\" }}");
        }

        [Fact(DisplayName = "Parser FromConstant")]
        public void FromConstant()
        {
            string result = Parser.Parse<MyTestClass>(x => x.Bar == false);

            Assert.True(result == $"\"Bar\": {{ \"$eq\": false }}");
        }

        [Fact(DisplayName = "Parser WithNullValue")]
        public void WithNullValue()
        {
            string result = Parser.Parse<MyTestClass>(x => x.Bat == null);
            
            Assert.True(result == $"\"Bat\": {{ \"$eq\": null }}");
        }

        [Fact(DisplayName = "Parser GreaterThan")]
        public void GreaterThan()
        {
            string result = Parser.Parse<MyTestClass>(x => x.Baz > TestClass.Baz);

            Assert.True(result == $"\"Baz\": {{ \"$gt\": {TestClass.Baz} }}");
        }

        [Fact(DisplayName = "Parser GreaterThanOrEqualTo")]
        public void GreaterThanOrEqualTo()
        {
            string result = Parser.Parse<MyTestClass>(x => x.Baz >= TestClass.Baz);

            Assert.True(result == $"\"Baz\": {{ \"$gte\": {TestClass.Baz} }}");
        }

        [Fact(DisplayName = "Parser LesserThan")]
        public void LesserThan()
        {
            string result = Parser.Parse<MyTestClass>(x => x.Baz < TestClass.Baz);

            Assert.True(result == $"\"Baz\": {{ \"$lt\": {TestClass.Baz} }}");
        }

        [Fact(DisplayName = "Parser LesserThanOrEqualTo")]
        public void LesserThanOrEqualTo()
        {
            string result = Parser.Parse<MyTestClass>(x => x.Baz <= TestClass.Baz);

            Assert.True(result == $"\"Baz\": {{ \"$lte\": {TestClass.Baz} }}");
        }

        [Fact(DisplayName = "Parser NotEqual")]
        public void NotEqual()
        {
            string result = Parser.Parse<MyTestClass>(x => x.Baz != TestClass.Baz);

            Assert.True(result == $"\"Baz\": {{ \"$neq\": {TestClass.Baz} }}");
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
