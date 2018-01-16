using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace Davenport.Infrastructure
{
    public class JsonContent : ByteArrayContent
    {
        public JsonContent(object content, JsonConverter customConverter = null) : base(ToBytes(content, customConverter))
        {
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        static byte[] ToBytes(object content, JsonConverter customConverter = null)
        {
            var settings = new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore,
            };

            if (customConverter != null)
            {
                settings.Converters.Add(customConverter);
            }

            var rawData = JsonConvert.SerializeObject(content, settings);

            return Encoding.UTF8.GetBytes(rawData);
        }
    }
}