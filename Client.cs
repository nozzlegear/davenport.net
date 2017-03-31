using Davenport.Interfaces;
using Flurl;
using Flurl.Http;
using System;
using System.Threading.Tasks;

namespace Davenport
{
    public class Client<DocumentType> where DocumentType: ICouchDoc
    {
        private Configuration Config { get; }

        /// <summary>
        /// A warning event fired when CouchDB returns a warning with its response, or when Davenport detects a possible error.
        /// </summary>
        public event EventHandler<string> Warning;

        public Client(Configuration config)
        {
            Config = config ?? new Configuration();
        }

        protected IFlurlClient PrepareRequest(string path)
        {
            var client = Url.Combine(Config.CouchUrl, Config.DatabaseName, path).AllowAnyHttpStatus();

            if (! string.IsNullOrEmpty(Config.Username) && ! string.IsNullOrEmpty(Config.Password))
            {
                client = client.WithBasicAuth(Config.Username, Config.Password);
            }
            
            return client;
        }

        protected async Task CheckErrorAndGetBody()
        {

        }
        
        /**
         * Deletes the document with the given id and revision id.
         */
        public async Task DeleteAsync(string id, string rev)
        {
            if (string.IsNullOrEmpty(rev))
            {
                Warning?.Invoke(this, $"No revision specified for Davenport.DeleteAsync method with id ${id}. This may cause a document conflict error.");
            }

            
            //var result = await Axios.delete(this.databaseUrl + id, {
            //    params: { rev }
            //});

            //await CheckErrorAndGetBody(result);
        }
    }
}
