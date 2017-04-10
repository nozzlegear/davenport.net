using System;
using System.Linq;
using System.Net.Http;

namespace Davenport.Infrastructure
{
    public class DavenportException : Exception
    {
        public DavenportException(string message) : base(message)
        {

        }
        
        public int StatusCode { get; set; }

        public string StatusText { get; set; }

        public string Url { get; set; }

        public string ResponseBody { get; set; }

        public static DavenportException FromHttpResponseMessage(HttpResponseMessage response, string rawBody, string message = "")
        {
            var ex = new DavenportException(message)
            {
                StatusCode = (int) response.StatusCode,
                StatusText = response.ReasonPhrase,
                Url = response.Headers.GetValues("HOST").FirstOrDefault(),
                ResponseBody = rawBody,
            };

            return ex;
        }
    }
}
