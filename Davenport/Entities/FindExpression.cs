using System;
using System.Linq.Expressions;
using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class FindExpression
    {
        public FindExpression() {}

        public FindExpression(ExpressionType type, object value)
        {
            switch (type)
            {
                default:
                    throw new ArgumentException($"Unsupported operation type '{type}'.");

                case ExpressionType.Equal:
                    EqualTo = value;
                    break;

                case ExpressionType.NotEqual:
                    NotEqualTo = value;
                    break;

                case ExpressionType.GreaterThan:
                    GreaterThan = value;
                    break;
                
                case ExpressionType.GreaterThanOrEqual:
                    GreaterThanOrEqualTo = value;
                    break;

                case ExpressionType.LessThan:
                    LesserThan = value;
                    break;

                case ExpressionType.LessThanOrEqual:
                    LesserThanOrEqualTo = value;
                    break;
            }
        }

        [JsonProperty("$eq")]
        public object EqualTo { get; set; }
        
        [JsonProperty("$neq")]
        public object NotEqualTo { get; set; }
        
        [JsonProperty("$gt")]
        public object GreaterThan { get; set; }
        
        [JsonProperty("$gte")]
        public object GreaterThanOrEqualTo { get; set; }
        
        [JsonProperty("$lt")]
        public object LesserThan { get; set; }
        
        [JsonProperty("$lte")]
        public object LesserThanOrEqualTo { get; set; }
    }
}