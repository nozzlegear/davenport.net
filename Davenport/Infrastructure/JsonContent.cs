using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace Davenport.Infrastructure
{
    public class JsonContent : ByteArrayContent
    {
        public JsonContent(object content) : base (ToBytes(content))
        {
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        static byte[] ToBytes(object content)
        {
            var rawData = JsonConvert.SerializeObject(content, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore,
            });
            
            return Encoding.UTF8.GetBytes(rawData);
        }
    }
}