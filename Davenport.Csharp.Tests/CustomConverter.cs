using System;
using Newtonsoft.Json;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Davenport.Tests
{
    public class CustomConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(CustomDoc);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var j = JObject.Load(reader);
            JToken id = null;
            JToken rev = null;

            // Change _id property name to MyId
            if (j["_id"] != null)
            {
                id = j["_id"];

                j.Remove("_id");
                j.Add("MyId", id);
            }

            // Change the _rev property name to MyRev
            if (j["_rev"] != null)
            {
                rev = j["_rev"];

                j.Remove("_rev");
                j.Add("MyRev", rev);
            }

            var data = j.ToObject<CustomDocData>(serializer);

            return new CustomDoc(data)
            {
                Id = id == null ? "" : id.Value<string>(),
                Rev = rev == null ? "" : rev.Value<string>()
            };
        }

        public override void WriteJson(JsonWriter writer, object objValue, JsonSerializer serializer)
        {
            var value = objValue as CustomDoc;

            if (value == null)
            {
                serializer.Serialize(writer, null);
                return;
            }

            writer.WriteStartObject();

            // We know this class and can easily map the MyId and MyRev to couchdb's _id and _rev values
            var id = value.Data.MyId;
            var rev = value.Data.MyRev;

            if (!String.IsNullOrEmpty(id))
            {
                writer.WritePropertyName("_id");
                writer.WriteValue(id);
            }

            if (!String.IsNullOrEmpty(rev))
            {
                writer.WritePropertyName("_rev");
                writer.WriteValue(rev);
            }

            // Merge the doc's data property with the doc that's being serialized so they're at the top level.
            var props = value.Data.GetType().GetProperties().Where(p => p.Name != "MyId" && p.Name != "MyRev");

            foreach (var property in props)
            {
                writer.WritePropertyName(property.Name);
                // Let the serializer figure out how to serialize the property value
                serializer.Serialize(writer, property.GetValue(value.Data, null));
            }

            writer.WriteEndObject();
        }
    }
}