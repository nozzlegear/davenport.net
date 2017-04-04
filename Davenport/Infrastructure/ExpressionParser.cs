using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Davenport.Entities;

namespace Davenport.Infrastructure
{
	public static class ExpressionParser
	{
		public static Dictionary<string, FindExpression> Parse<DocumentType>(Expression<Func<DocumentType, bool>> expression)
		{
			var bod = expression.Body as BinaryExpression;

			if (bod == null)
			{
				throw new ArgumentException($"Invalid expression. Expression must be in the form of e.g. x => x.Foo == 5 and must use the document parameter passed in.");
			}
			
			var body = expression.Body as BinaryExpression;

			if (body == null)
			{
				throw new ArgumentException($"Expression body could not be converted to a binary expression.");
			}

			switch (body.NodeType)
			{
				default:
					throw new ArgumentException($"Davenport currently only supports == expressions. Type received: {body.NodeType}.");

				case ExpressionType.Or:
				case ExpressionType.OrElse:
					throw new ArgumentException($"CouchDB's find method does not support || expressions. We recommend constructing a view instead.");

				// Supported types:
				case ExpressionType.Equal:
				case ExpressionType.NotEqual:
				case ExpressionType.GreaterThan:
				case ExpressionType.GreaterThanOrEqual:
				case ExpressionType.LessThan:
				case ExpressionType.LessThanOrEqual:
					break;
			}

			var parts = GetExpressionParts(body);

			return new Dictionary<string, FindExpression>()
			{
				{ parts.PropName, new FindExpression(body.NodeType, parts.Value) }
			};
		}
		
		static object InvokeExpression(Expression exp)
		{
			object val = Expression.Lambda(exp).Compile().DynamicInvoke();

			return val;
		}
		
		static (bool IsPropName, object Value) GetMemberValue(MemberExpression member)
		{
			try
			{
				return (false, InvokeExpression(member));
			}
			catch (Exception ex)
			{
				return (true, member.Member.Name);
			}
		}

		static (bool IsPropName, object Value) GetExpressionValue(Expression exp)
		{
			switch (exp.NodeType)
			{
				case ExpressionType.Constant:
					var constVal = ((ConstantExpression) exp).Value;
					
					return (false, constVal);

				case ExpressionType.MemberAccess:
                    return GetMemberValue((MemberExpression)exp);
					
				case ExpressionType.Convert:
					var unary = (UnaryExpression) exp;
					
					return GetExpressionValue(unary.Operand);
					
				default: throw new ArgumentException($"Expression type {exp.NodeType} is invalid.");
			}
		}

		static (string PropName, object Value) GetExpressionParts(BinaryExpression exp)
		{
			var leftValue = GetExpressionValue(exp.Left);
			var rightValue  = GetExpressionValue(exp.Right);
			
			if (leftValue.IsPropName)
			{
				return (leftValue.Value as string, rightValue.Value);
			}

			return (rightValue.Value as string, leftValue.Value);
		}
	}
}