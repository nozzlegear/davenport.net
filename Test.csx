static void Main()
{
    var c = new Client<MyTestClass>();
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
    c.Find(x => x.Bat > myClass.Baz && x.Bat < myClass.Baz && x.Bat == myClass.Baz);
}

class Client<DocumentType>
{
    public void Find(Expression<Func<DocumentType, bool>> expression)
    {
        var bod = expression.Body as BinaryExpression;

		if (bod == null)
		{
			throw new ArgumentException($"Invalid expression. Expression must be in the form of e.g. x => x.Foo == 5 and must use the document parameter passed in.");
		}
        
        Console.WriteLine("Binary Expression Type: {0}", bod.NodeType);
        Console.WriteLine("Method to be called: {0}", bod.Method);
        Console.WriteLine("Return Type: {0}", expression.ReturnType);
        Console.WriteLine(ParseExpression(expression));
        Console.WriteLine(bod);
    }
    
    static string ValueToString(object memberValue)
	{
		if (memberValue == null)
		{
			return $"null";
		}
		
        Type t = memberValue.GetType();
    
        if (t == typeof(string))
        {
            string value = memberValue.ToString();
            
            return $"\"{value}\"";
        }
        
        if (t == typeof(bool))
        {
            bool value = (bool) memberValue;
            
            return  value.ToString().ToLower();
        }
        
        if (t == typeof(int))
        {
            int value = (int) memberValue;
            
            return value.ToString();
        }
        
        throw new ArgumentException($"Value type {t} is not supported.");
    }
    
    static string GetMemberValue(MemberExpression member)
    {
		try
		{
			object val = Expression.Lambda(member).Compile().DynamicInvoke();
			
			return ValueToString(val);
		}
		catch (Exception)
		{
			return member.Member.Name;
		}
    }

    static string GetExpressionValue(Expression exp)
    {
        switch (exp.NodeType)
        {
            case ExpressionType.Constant:
                var constVal = ((ConstantExpression) exp).Value;
                
                return ValueToString(constVal);

            case ExpressionType.MemberAccess:
                return GetMemberValue((MemberExpression) exp);
				
			case ExpressionType.Convert:
				var unary = (UnaryExpression) exp;
				
				return GetExpressionValue(unary.Operand);
				
            default: throw new ArgumentException($"Expression type {exp.NodeType} is invalid.");
        }
    }

	static string ParseExpression<T>(Expression<Func<T, bool>> expression)
	{
		var body = expression.Body as BinaryExpression;

		if (body == null)
		{
			throw new ArgumentException($"Expression body could not be converted to a binary expression.");
		}

		string operation;

		switch (body.NodeType)
		{
			default:
				Console.WriteLine(body);
				throw new ArgumentException($"Davenport currently only supports == expressions. Type received: {body.NodeType}.");

			case ExpressionType.Or:
			case ExpressionType.OrElse:
				throw new ArgumentException($"CouchDB's find method does not support || expressions. We recommend constructing a view instead.");

			case ExpressionType.Equal:
				operation = "$eq";
				break;

			case ExpressionType.NotEqual:
				operation = "$neq";
				break;
				
			case ExpressionType.GreaterThan:
				operation = "$gt";
				break;
				
			case ExpressionType.GreaterThanOrEqual:
				operation = "$gte";
				break;
				
			case ExpressionType.LessThan:
				operation = "$lt";
				break;
				
			case ExpressionType.LessThanOrEqual:
				operation = "$lte";
				break;
		}

		var left = GetExpressionValue(body.Left);
		var right = GetExpressionValue(body.Right);

		return $"\"{left}\": {{ \"{operation}\": {right} }}";
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