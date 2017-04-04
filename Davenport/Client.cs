using Davenport.Interfaces;
using Flurl;
using Flurl.Http;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using Davenport.Infrastructure;
using Newtonsoft.Json;
using Davenport.Entities;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq.Expressions;

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

        private async Task<IEnumerable<DocumentType>> FindAsync(object selector, Dictionary<string, object> options)
        {
            var request = PrepareRequest("_find");
            
            // Make sure the selector is occupying the selector key
            if (options.ContainsKey("selector"))
            {
                options.Remove("selector");
            }
            
            options.Add("selector", selector);

            var content = new JsonContent(options);

            return await ExecuteRequestAsync<List<DocumentType>>(request, HttpMethod.Post, content);
        }

        /// <summary>
        /// Searches for documents matching the given selector. NOTE: Davenport currently only supports simple 1 argument selectors, e.g. x => x.Foo == "value".
        /// </summary>
        public async Task<IEnumerable<DocumentType>> FindAsync(Expression<Func<DocumentType, bool>> expression, FindOptions options = null)
        {
            return await FindAsync(ExpressionParser.Parse(expression), options?.ToDictionary() ?? new Dictionary<string, object>());
        }

        /// <summary>
        /// Searches for documents matching the given selector.
        /// </summary>
        public async Task<IEnumerable<DocumentType>> FindAsync(object selector, FindOptions options = null)
        {
            return await FindAsync(selector, options?.ToDictionary() ?? new Dictionary<string, object>());
        }

        /// <summary>
        /// Searches for documents matching the given selector.
        /// </summary>
        public async Task<IEnumerable<DocumentType>> FindAsync(Dictionary<string, FindExpression> selector, FindOptions options = null)
        {
            return await FindAsync(selector, options?.ToDictionary() ?? new Dictionary<string, object>());
        }

        public async Task<ListResponse<DocumentType>> ListWithDocsAsync(ListOptions options = null)
        {
            var request = PrepareRequest("_all_docs");

            if (options != null)
            {
                request.Url.QueryParams.AddRange(options.ToQueryParameters());
            }

            request.Url.SetQueryParam("include_docs", true);

            var result = await ExecuteRequestAsync<ListResponse<JToken>>(request, HttpMethod.Get);
            var rows = new List<ListedRow<DocumentType>>();
            var designDocs = new List<ListedRow<object>>();

            // Will probably need to split out the DesignDocs as they won't deserialize properly.
            foreach (var designDoc in result.Rows.Where(r => r.Id.StartsWith("_design")))
            {
                var row = new ListedRow<object>()
                {
                    Id = designDoc.Id,
                    Key = designDoc.Key,
                    Value = designDoc.Value,
                    Doc = designDoc.Doc.ToObject<object>(),
                };

                designDocs.Add(row);
            }

            foreach (var doc in result.Rows.Where(r => !r.Id.StartsWith("_design")))
            {
                var row = new ListedRow<DocumentType>()
                {
                    Id = doc.Id,
                    Key = doc.Key,
                    Value = doc.Value,
                    Doc = doc.Doc.ToObject<DocumentType>(),
                };

                rows.Add(row);
            }

            return new ListResponse<DocumentType>()
            {
                TotalRows = result.TotalRows,
                Offset = result.Offset,
                DesignDocs = designDocs,
                Rows = rows,
            };
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

        /// <summary>
        /// Executes a view with the given <paramref name="designDocName" /> and <paramref name="viewName" />
        /// </summary>
        public async Task<ListResponse<ReturnType>> ViewAsync<ReturnType>(string designDocName, string viewName, ViewOptions options = null)
        {
            var request = PrepareRequest($"_design/{designDocName}/_view/{viewName}");

            if (options != null)
            {
                request.Url.QueryParams.AddRange(options.ToQueryParameters());
            }

            return await ExecuteRequestAsync<ListResponse<ReturnType>>(request, HttpMethod.Get);
        }
    }
}
