using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Flurl;
using Newtonsoft.Json;

namespace Davenport.Infrastructure
{
    public class Serializable
    {
        /// <summary>
        /// Converts this object to a dictionary, using each property's <see cref='Newtonsoft.Json.JsonPropertyAttribute' /> value as the key.
        /// </summary>
        public virtual Dictionary<string, object> ToDictionary()
        {
            var output = new Dictionary<string, object>();

            foreach (PropertyInfo property in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = property.GetValue(this, null);
                string propName = property.Name;

                if (value == null)
                {
                    continue;
                }

                if (property.CustomAttributes.Any(att => att.AttributeType == typeof(JsonPropertyAttribute)))
                {
                    // Use the JsonPropertyName instead of the C# property namespace
                    var att = property.GetCustomAttributes(typeof(JsonPropertyAttribute), false).Cast<JsonPropertyAttribute>().FirstOrDefault();

                    propName = att?.PropertyName ?? property.Name;
                }

                output.Add(propName, value);
            }

            return output;
        }

        /// <summary>
        /// Converts this object to a list of <see cref='QueryParameter' />. 
        /// </summary>
        public virtual IEnumerable<QueryParameter> ToQueryParameters()
        {
            var kvps = ToDictionary();
            
            return kvps.Select(kvp => new QueryParameter(kvp.Key, kvp.Value));
        }
    }
}