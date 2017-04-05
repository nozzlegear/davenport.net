using System;
using System.Linq;
using Davenport.Infrastructure;
using Xunit;

namespace Davenport.Tests
{
    public class ExpressionParserTests
    {
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
			var result = ExpressionParser.Parse<MyTestClass>(x => x.Baz == TestClass.Baz);

            Assert.True(result.Any(kvp => kvp.Key == "Baz" && (int) kvp.Value.EqualTo == TestClass.Baz));
        }

        [Fact(DisplayName = "Parser FromVariable")]
        public void FromVariable()
        {
            string searchString = "test";
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Foo == searchString);

            Assert.True(result.Any(kvp => kvp.Key == "Foo" && (string) kvp.Value.EqualTo == searchString));
        }

        [Fact(DisplayName = "Parser FromConstant")]
        public void FromConstant()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Bar == false);

            Assert.True(result.Any(kvp => kvp.Key == "Bar" && (bool) kvp.Value.EqualTo == false));
        }

        [Fact(DisplayName = "Parser WithNullValue")]
        public void WithNullValue()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Bat == null);
            
            Assert.True(result.Any(kvp => kvp.Key == "Bat" && kvp.Value.EqualTo == null));
        }

        [Fact(DisplayName = "Parser GreaterThan")]
        public void GreaterThan()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Baz > TestClass.Baz);

            Assert.True(result.Any(kvp => kvp.Key == "Baz" && (int) kvp.Value.GreaterThan == TestClass.Baz));
        }

        [Fact(DisplayName = "Parser GreaterThanOrEqualTo")]
        public void GreaterThanOrEqualTo()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Baz >= TestClass.Baz);

            Assert.True(result.Any(kvp => kvp.Key == "Baz" && (int) kvp.Value.GreaterThanOrEqualTo == TestClass.Baz));
        }

        [Fact(DisplayName = "Parser LesserThan")]
        public void LesserThan()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Baz < TestClass.Baz);

            Assert.True(result.Any(kvp => kvp.Key == "Baz" && (int) kvp.Value.LesserThan == TestClass.Baz));
        }

        [Fact(DisplayName = "Parser LesserThanOrEqualTo")]
        public void LesserThanOrEqualTo()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Baz <= TestClass.Baz);

            Assert.True(result.Any(kvp => kvp.Key == "Baz" && (int) kvp.Value.LesserThanOrEqualTo == TestClass.Baz));
        }

        [Fact(DisplayName = "Parser NotEqual")]
        public void NotEqual()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Baz != TestClass.Baz);

            Assert.True(result.Any(kvp => kvp.Key == "Baz" && (int) kvp.Value.NotEqualTo == TestClass.Baz));
        }

        [Fact(DisplayName = "Parser Backwards")]
        public void Backwards()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => 5 == x.Baz);

            Assert.True(result.Any(kvp => kvp.Key == "Baz" && (int)kvp.Value.EqualTo == 5));
        }
    }
}
