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

        [Fact(DisplayName = "Parser FromClass"), Trait("Category", "Parser")]
        public void FromClass()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Baz == TestClass.Baz);

            Assert.Contains(result, kvp => kvp.Key == "Baz" && (int)kvp.Value.EqualTo == TestClass.Baz);
        }

        [Fact(DisplayName = "Parser FromVariable"), Trait("Category", "Parser")]
        public void FromVariable()
        {
            string searchString = "test";
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Foo == searchString);

            Assert.Contains(result, kvp => kvp.Key == "Foo" && (string)kvp.Value.EqualTo == searchString);
        }

        [Fact(DisplayName = "Parser FromConstant"), Trait("Category", "Parser")]
        public void FromConstant()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Bar == false);

            Assert.Contains(result, kvp => kvp.Key == "Bar" && (bool)kvp.Value.EqualTo == false);
        }

        [Fact(DisplayName = "Parser WithNullValue"), Trait("Category", "Parser")]
        public void WithNullValue()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Bat == null);

            Assert.Contains(result, kvp => kvp.Key == "Bat" && kvp.Value.EqualTo == null);
        }

        [Fact(DisplayName = "Parser GreaterThan"), Trait("Category", "Parser")]
        public void GreaterThan()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Baz > TestClass.Baz);

            Assert.Contains(result, kvp => kvp.Key == "Baz" && (int)kvp.Value.GreaterThan == TestClass.Baz);
        }

        [Fact(DisplayName = "Parser GreaterThanOrEqualTo"), Trait("Category", "Parser")]
        public void GreaterThanOrEqualTo()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Baz >= TestClass.Baz);

            Assert.Contains(result, kvp => kvp.Key == "Baz" && (int)kvp.Value.GreaterThanOrEqualTo == TestClass.Baz);
        }

        [Fact(DisplayName = "Parser LesserThan"), Trait("Category", "Parser")]
        public void LesserThan()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Baz < TestClass.Baz);

            Assert.Contains(result, kvp => kvp.Key == "Baz" && (int)kvp.Value.LesserThan == TestClass.Baz);
        }

        [Fact(DisplayName = "Parser LesserThanOrEqualTo"), Trait("Category", "Parser")]
        public void LesserThanOrEqualTo()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Baz <= TestClass.Baz);

            Assert.Contains(result, kvp => kvp.Key == "Baz" && (int)kvp.Value.LesserThanOrEqualTo == TestClass.Baz);
        }

        [Fact(DisplayName = "Parser NotEqual"), Trait("Category", "Parser")]
        public void NotEqual()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => x.Baz != TestClass.Baz);

            Assert.Contains(result, kvp => kvp.Key == "Baz" && (int)kvp.Value.NotEqualTo == TestClass.Baz);
        }

        [Fact(DisplayName = "Parser Backwards"), Trait("Category", "Parser")]
        public void Backwards()
        {
            var result = ExpressionParser.Parse<MyTestClass>(x => 5 == x.Baz);

            Assert.Contains(result, kvp => kvp.Key == "Baz" && (int)kvp.Value.EqualTo == 5);
        }
    }
}
