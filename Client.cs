using Davenport.Interfaces;
using Flurl;
using Flurl.Http;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using Davenport.Infrastructure;
using Newtonsoft.Json;
using Davenport.Entities;

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

        protected IFlurlClient PrepareRequest(string path, string rev = null)
        {
            var client = Url.Combine(Config.CouchUrl, Config.DatabaseName, path).AllowAnyHttpStatus();

            if (! string.IsNullOrEmpty(Config.Username) && ! string.IsNullOrEmpty(Config.Password))
            {
                client = client.WithBasicAuth(Config.Username, Config.Password);
            }

            if (! string.IsNullOrEmpty(rev))
            {
                client.Url.QueryParams.Add("rev", rev);
            }
            
            return client;
        }

        /// <summary>
        /// Takes a Flurl request client and executes it, sending along any provided content.
        /// </summary>
        /// <param name="client">The Flurl request client. This will be automatically disposed.</param>
        /// <param name="method">Method used for the request.</param>
        /// <param name="content">Optional content sent along with POST or PUT requests.</param>
        protected async Task<T> ExecuteRequestAsync<T>(IFlurlClient client, HttpMethod method, HttpContent content = null)
        {
            using (client)
            {
                var request = client.SendAsync(method, content);
                var result = await request;
                var rawBody = await request.ReceiveString();

                if (! result.IsSuccessStatusCode)
                {
                    var message = $"Error with {client} request for CouchDB database {Config.DatabaseName} at {client.Url.ToString()}. {result.StatusCode} {result.ReasonPhrase}";
                    var ex = new DavenportException(message)
                    {
                        StatusCode = (int) result.StatusCode,
                        StatusText = result.ReasonPhrase,
                        ResponseBody = rawBody,
                        Url = client.Url.ToString(),   
                    };
                    
                    throw ex;
                }

                return JsonConvert.DeserializeObject<T>(rawBody);
            }
        }
        
        /// <summary>
        /// Gets a document with the given <paramref name="id" /> and optional <paramref name="rev" />.
        /// </summary>
        public async Task<DocumentType> GetAsync(string id, string rev = null)
        {
            var request = PrepareRequest(id, rev);

            return await ExecuteRequestAsync<DocumentType>(request, HttpMethod.Get);
        }

        /// <summary>
        /// Creates a document and assigns a random id.
        /// </summary>
        public async Task<PostPutCopyResponse> PostAsync(DocumentType doc)
        {
            var content = new JsonContent(doc);
            var request = PrepareRequest("");

            return await ExecuteRequestAsync<PostPutCopyResponse>(request, HttpMethod.Post, content);
        }

        /// <summary>
        /// Updates or creates a document with the given <paramref name="id" />.
        /// </summary>
        public async Task<PostPutCopyResponse> PutAsync(string id, DocumentType doc, string rev = null)
        {
            var content = new JsonContent(doc);
            var request = PrepareRequest(id, rev);

            return await ExecuteRequestAsync<PostPutCopyResponse>(request, HttpMethod.Put, content);
        }

        /// <summary>
        /// Checks whether a document with the given <paramref name="id" /> and optional <paramref name="rev" /> exists.
        /// </summary>
        public async Task<bool> ExistsAsync(string id, string rev = null)
        {
            using (var request = PrepareRequest(id, rev))
            {
                var result = await request.HeadAsync();

                return result.IsSuccessStatusCode;
            }
        }

        /// <summary>
        /// Copies the document with the given <paramref name="id" /> and assigns the <paramref name="newId" /> to the copy.
        /// </summary>
        public async Task<PostPutCopyResponse> CopyAsync(string id, string newId)
        {
            var request = PrepareRequest(id).WithHeader("Destination", newId);
            var method = new HttpMethod("COPY");

            return await ExecuteRequestAsync<PostPutCopyResponse>(request, method);
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

            var request = PrepareRequest(id, rev);

            await ExecuteRequestAsync<object>(request, HttpMethod.Delete);
        }
    }
}
