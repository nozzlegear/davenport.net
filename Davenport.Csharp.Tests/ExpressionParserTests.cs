using System;
using System.Collections.Generic;
using System.Linq;
using Davenport.Csharp;
using Microsoft.FSharp.Collections;
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

        /// <summary>
        /// Checks that a KeyValuePair from the ExpressionParser.Parse dictionary contains the given key and a FindOperator that matches the given function.
        /// </summary>
        private bool FindMatch (KeyValuePair<string, FSharpList<Types.FindOperator>> kvp, string key, Func<Types.FindOperator, bool> fn)
        {
            return kvp.Key == key && kvp.Value.Any(fn);
        }

        [Fact(DisplayName = "Parser FromClass"), Trait("Category", "Parser")]
        public void FromClass()
        {
            var result = ExpressionParser.parse<MyTestClass>(x => x.Baz == TestClass.Baz);

            Assert.Contains(result, kvp => FindMatch(kvp, "Baz", op => op.IsEqualTo && ((Types.FindOperator.EqualTo) op).Item == (object) TestClass.Baz));
        }

        [Fact(DisplayName = "Parser FromVariable"), Trait("Category", "Parser")]
        public void FromVariable()
        {
            string searchString = "test";
            var result = ExpressionParser.parse<MyTestClass>(x => x.Foo == searchString);

            Assert.Contains(result, kvp => FindMatch(kvp, "Foo", op => op.IsEqualTo && ((Types.FindOperator.EqualTo) op).Item == (object) searchString));
        }

        [Fact(DisplayName = "Parser FromConstant"), Trait("Category", "Parser")]
        public void FromConstant()
        {
            var result = ExpressionParser.parse<MyTestClass>(x => x.Bar == false);

            Assert.Contains(result, kvp => FindMatch(kvp, "Bar", op => op.IsEqualTo && ((Types.FindOperator.EqualTo) op).Item == (object) false));
        }

        [Fact(DisplayName = "Parser WithNullValue"), Trait("Category", "Parser")]
        public void WithNullValue()
        {
            var result = ExpressionParser.parse<MyTestClass>(x => x.Bat == null);

            Assert.Contains(result, kvp => FindMatch(kvp, "Bat", op => op.IsEqualTo && ((Types.FindOperator.EqualTo) op).Item == null));
        }

        [Fact(DisplayName = "Parser GreaterThan"), Trait("Category", "Parser")]
        public void GreaterThan()
        {
            var result = ExpressionParser.parse<MyTestClass>(x => x.Baz > TestClass.Baz);

            Assert.Contains(result, kvp => FindMatch(kvp, "Baz", op => op.IsGreaterThan && ((Types.FindOperator.GreaterThan) op).Item == (object) TestClass.Baz));
        }

        [Fact(DisplayName = "Parser GreaterThanOrEqualTo"), Trait("Category", "Parser")]
        public void GreaterThanOrEqualTo()
        {
            var result = ExpressionParser.parse<MyTestClass>(x => x.Baz >= TestClass.Baz);

            Assert.Contains(result, kvp => FindMatch(kvp, "Baz", op => op.IsGreaterThanOrEqualTo && ((Types.FindOperator.GreaterThanOrEqualTo) op).Item == (object) TestClass.Baz));
        }

        [Fact(DisplayName = "Parser LesserThan"), Trait("Category", "Parser")]
        public void LesserThan()
        {
            var result = ExpressionParser.parse<MyTestClass>(x => x.Baz < TestClass.Baz);

            Assert.Contains(result, kvp => FindMatch(kvp, "Baz", op => op.IsLesserThan && ((Types.FindOperator.LesserThan) op).Item == (object) TestClass.Baz));
        }

        [Fact(DisplayName = "Parser LesserThanOrEqualTo"), Trait("Category", "Parser")]
        public void LesserThanOrEqualTo()
        {
            var result = ExpressionParser.parse<MyTestClass>(x => x.Baz <= TestClass.Baz);

            Assert.Contains(result, kvp => FindMatch(kvp, "Baz", op => op.IsLessThanOrEqualTo && ((Types.FindOperator.LessThanOrEqualTo) op).Item == (object) TestClass.Baz));
        }

        [Fact(DisplayName = "Parser NotEqual"), Trait("Category", "Parser")]
        public void NotEqual()
        {
            var result = ExpressionParser.parse<MyTestClass>(x => x.Baz != TestClass.Baz);

            Assert.Contains(result, kvp => FindMatch(kvp, "Baz", op => op.IsNotEqualTo && ((Types.FindOperator.NotEqualTo) op).Item == (object) TestClass.Baz));
        }

        [Fact(DisplayName = "Parser Backwards"), Trait("Category", "Parser")]
        public void Backwards()
        {
            var result = ExpressionParser.parse<MyTestClass>(x => 5 == x.Baz);

            Assert.Contains(result, kvp => FindMatch(kvp, "Baz", op => op.IsEqualTo && ((Types.FindOperator.EqualTo) op).Item == (object) 5));
        }
    }
}
