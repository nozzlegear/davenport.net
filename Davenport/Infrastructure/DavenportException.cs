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
    }
}
