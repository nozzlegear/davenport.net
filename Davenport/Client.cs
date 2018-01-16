using Flurl;
using Flurl.Http;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using Davenport.Entities;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq.Expressions;
using Davenport.Infrastructure;

namespace Davenport
{
    public class Client<DocumentType> where DocumentType : CouchDoc
    {
        private Configuration Config { get; }

        /// <param name="couchUrl">URL string pointing to your CouchDB installation, e.g. http://localhost:5984. Should not include your database name.</param>
        /// <param name="databaseName">The name of your database. Should be URL compatible.</param>
        public Client(string couchUrl, string databaseName)
        {
            Config = new Configuration(couchUrl, databaseName);
        }

        public Client(Configuration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            Config = config;
        }

        protected IFlurlRequest PrepareRequest(string path, string rev = null)
        {
            var request = Url.Combine(Config.CouchUrl, Config.DatabaseName, path).AllowAnyHttpStatus();

            if (!string.IsNullOrEmpty(Config.Username) && !string.IsNullOrEmpty(Config.Password))
            {
                request = request.WithBasicAuth(Config.Username, Config.Password);
            }

            if (!string.IsNullOrEmpty(rev))
            {
                request.Url.QueryParams.Add("rev", rev);
            }

            return request;
        }

        /// <summary>
        /// Checks a <see cref="HttpResponseMessage" /> for errors and throws a <see cref="DavenportException" /> if found.
        /// </summary>
        protected void CheckAndThrowIfError(IFlurlRequest request, HttpResponseMessage result, string rawBody)
        {
            if (!result.IsSuccessStatusCode)
            {
                var message = $"Error with {request} request for CouchDB database {Config.DatabaseName} at {request.Url.ToString()}. {result.StatusCode} {result.ReasonPhrase}";
                var ex = new DavenportException(message)
                {
                    StatusCode = (int)result.StatusCode,
                    StatusText = result.ReasonPhrase,
                    ResponseBody = rawBody,
                    Url = request.Url.ToString(),
                };

                throw ex;
            }
        }

        /// <summary>
        /// Takes a Flurl request and executes it, sending along any provided content.
        /// </summary>
        /// <param name="flurlRequest">The Flurl request.</param>
        /// <param name="method">Method used for the request.</param>
        /// <param name="content">Optional content sent along with POST or PUT requests. Will be converted to JSON using the Configuration object's custom json converter.</param>
        protected async Task<T> ExecuteRequestAsync<T>(IFlurlRequest flurlRequest, HttpMethod method, object content = null)
        {
            HttpContent body = content == null ? null : new JsonContent(content, Config.Converter);
            var request = flurlRequest.SendAsync(method, body);
            var result = await request;
            var rawBody = await request.ReceiveString();

            CheckAndThrowIfError(flurlRequest, result, rawBody);

            // Use the custom json converter if it exists
            if (Config.Converter != null)
            {
                return JsonConvert.DeserializeObject<T>(rawBody, Config.Converter);
            }

            return JsonConvert.DeserializeObject<T>(rawBody);
        }

        /// <summary>
        /// Gets a document with the given <paramref name="id" /> and optional <paramref name="rev" />.
        /// </summary>
        public async Task<DocumentType> GetAsync(string id, string rev = null)
        {
            var request = PrepareRequest(id, rev);

            return await ExecuteRequestAsync<DocumentType>(request, HttpMethod.Get);
        }

        private async Task<IEnumerable<DocumentType>> _FindAsync(object selector, Dictionary<string, object> options)
        {
            var request = PrepareRequest("_find");

            // Make sure the selector is occupying the selector key
            if (options.ContainsKey("selector"))
            {
                options.Remove("selector");
            }

            options.Add("selector", selector);

            var result = await ExecuteRequestAsync<JToken>(request, HttpMethod.Post, options);
            var warning = result.SelectToken("warning", false);

            if (warning?.HasValues == true)
            {
                Config.InvokeWarningEvent(this, warning.Value<string>());
            }

            return result.SelectToken("docs").ToObject<List<DocumentType>>();
        }

        /// <summary>
        /// Searches for documents matching the given selector. NOTE: Davenport currently only supports simple 1 argument selectors, e.g. x => x.Foo == "value".
        /// </summary>
        public async Task<IEnumerable<DocumentType>> FindAsync(Expression<Func<DocumentType, bool>> expression, FindOptions options = null)
        {
            return await _FindAsync(ExpressionParser.Parse(expression), options?.ToDictionary() ?? new Dictionary<string, object>());
        }

        /// <summary>
        /// Searches for documents matching the given selector.
        /// </summary>
        public async Task<IEnumerable<DocumentType>> FindAsync(object selector, FindOptions options = null)
        {
            return await _FindAsync(selector, options?.ToDictionary() ?? new Dictionary<string, object>());
        }

        /// <summary>
        /// Searches for documents matching the given selector.
        /// </summary>
        public async Task<IEnumerable<DocumentType>> FindAsync(Dictionary<string, FindExpression> selector, FindOptions options = null)
        {
            return await _FindAsync(selector, options?.ToDictionary() ?? new Dictionary<string, object>());
        }

        /// <summary>
        /// Retrieves a count of all documents in the database. NOTE: this count includes design documents.
        /// </summary>
        public async Task<int> CountAsync()
        {
            var list = await ListWithoutDocsAsync(new ListOptions()
            {
                Limit = 0
            });

            return list.TotalRows;
        }

        private async Task<int> _CountBySelectorAsync(object selector)
        {
            // Selectors must use the Find API, which means they must return documents too. Limit the bandwidth by just returning _id.
            var result = await FindAsync(selector, new FindOptions()
            {
                Fields = new string[] { "_id" }
            });

            return result.Count();
        }

        /// <summary>
        /// Retrieves a count of all documents matching the given selector. NOTE: Davenport currently only supports simple 1 argument selectors, e.g. x => x.Foo == "value".
        /// </summary>
        public async Task<int> CountBySelectorAsync(Expression<Func<DocumentType, bool>> expression)
        {
            return await _CountBySelectorAsync(ExpressionParser.Parse(expression));
        }

        /// <summary>
        /// Retrieves a count of all documents matching the given selector.
        /// </summary>
        public async Task<int> CountBySelectorAsync(object selector)
        {
            return await _CountBySelectorAsync(selector);
        }

        /// <summary>
        /// Retrieves a count of all documents matching the given selector.
        /// </summary>
        public async Task<int> CountBySelectorAsync(Dictionary<string, FindExpression> selector)
        {
            return await _CountBySelectorAsync(selector);
        }

        (List<ListedRow<object>> DesignDocs, List<ListedRow<T>> Docs) _SortDocuments<T>(ListResponse<JToken> docs)
        {
            var rows = new List<ListedRow<T>>();
            var designDocs = new List<ListedRow<object>>();

            // Will probably need to split out the DesignDocs as they won't deserialize properly.
            foreach (var designDoc in docs.Rows.Where(r => r.Id.StartsWith("_design")))
            {
                var row = new ListedRow<object>()
                {
                    Id = designDoc.Id,
                    Key = designDoc.Key,
                    Value = designDoc.Value,
                    Doc = designDoc.Doc?.ToObject<object>(),
                };

                designDocs.Add(row);
            }

            foreach (var doc in docs.Rows.Where(r => !r.Id.StartsWith("_design")))
            {
                var row = new ListedRow<T>()
                {
                    Id = doc.Id,
                    Key = doc.Key,
                    Value = doc.Value,
                    Doc = doc.Doc != null ? doc.Doc.ToObject<T>() : default(T)
                };

                rows.Add(row);
            }

            return (DesignDocs: designDocs, Docs: rows);
        }

        /// <summary>
        /// Lists all documents on the database.
        /// </summary>
        public async Task<ListResponse<DocumentType>> ListWithDocsAsync(ListOptions options = null)
        {
            var request = PrepareRequest("_all_docs");

            if (options != null)
            {
                request.Url.QueryParams.AddRange(options.ToQueryParameters());
            }

            request.Url.SetQueryParam("include_docs", true);

            var result = await ExecuteRequestAsync<ListResponse<JToken>>(request, HttpMethod.Get);
            var sort = _SortDocuments<DocumentType>(result);

            return new ListResponse<DocumentType>()
            {
                TotalRows = result.TotalRows,
                Offset = result.Offset,
                DesignDocs = sort.DesignDocs,
                Rows = sort.Docs,
            };
        }

        /// <summary>
        /// Lists all documents on the database, but does not return the documents themselves.
        /// </summary>
        public async Task<ListResponse<Revision>> ListWithoutDocsAsync(ListOptions options = null)
        {
            var request = PrepareRequest("_all_docs");

            if (options != null)
            {
                request.Url.QueryParams.AddRange(options.ToQueryParameters());
            }

            request.Url.SetQueryParam("include_docs", false);

            var result = await ExecuteRequestAsync<ListResponse<JToken>>(request, HttpMethod.Get);
            var sort = _SortDocuments<Revision>(result);

            // Docs weren't included, so copy the Revision value to the doc instead
            sort.Docs.ForEach(doc => doc.Doc = doc.Value);
            sort.DesignDocs.ForEach(doc => doc.Doc = doc.Value);

            return new ListResponse<Revision>()
            {
                TotalRows = result.TotalRows,
                Offset = result.Offset,
                DesignDocs = sort.DesignDocs,
                Rows = sort.Docs,
            };
        }

        /// <summary>
        /// Creates a document and assigns a random id.
        /// </summary>
        public async Task<PostPutCopyResponse> PostAsync(DocumentType doc)
        {
            var request = PrepareRequest("");

            return await ExecuteRequestAsync<PostPutCopyResponse>(request, HttpMethod.Post, doc);
        }

        /// <summary>
        /// Updates or creates a document with the given <paramref name="id" />.
        /// </summary>
        public async Task<PostPutCopyResponse> PutAsync(string id, DocumentType doc, string rev = null)
        {
            var request = PrepareRequest(id, rev);

            return await ExecuteRequestAsync<PostPutCopyResponse>(request, HttpMethod.Put, doc);
        }

        /// <summary>
        /// Checks whether a document with the given <paramref name="id" /> and optional <paramref name="rev" /> exists.
        /// </summary>
        public async Task<bool> ExistsAsync(string id, string rev = null)
        {
            var request = PrepareRequest(id, rev);
            var result = await request.HeadAsync();

            return result.IsSuccessStatusCode;
        }

        private async Task<bool> _ExistsBySelector(object selector)
        {
            var result = await FindAsync(selector, new FindOptions()
            {
                Fields = new string[] { "_id" },
                Limit = 1
            });

            return result.Count() > 0;
        }

        /// <summary>
        /// Checks that a document matching the given selector exists. NOTE: Davenport currently only supports simple 1 argument selectors, e.g. x => x.Foo == "value".
        /// </summary>
        public async Task<bool> ExistsBySelector(Expression<Func<DocumentType, bool>> expression)
        {
            return await _ExistsBySelector(ExpressionParser.Parse(expression));
        }

        /// <summary>
        /// Checks that a document matching the given selector exists.
        /// </summary>
        public async Task<bool> ExistsBySelector(object selector)
        {
            return await _ExistsBySelector(selector);
        }

        /// <summary>
        /// Checks that a document matching the given selector exists.
        /// </summary>
        public async Task<bool> ExistsBySelector(Dictionary<string, FindExpression> selector)
        {
            return await _ExistsBySelector(selector);
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
                Config.InvokeWarningEvent(this, $"No revision specified for Davenport.DeleteAsync method with id ${id}. This may cause a document conflict error.");
            }

            var request = PrepareRequest(id, rev);

            await ExecuteRequestAsync<object>(request, HttpMethod.Delete);
        }

        /// <summary>
        /// Executes a view with the given <paramref name="designDocName" /> and <paramref name="viewName" />
        /// </summary>
        public async Task<IEnumerable<ViewResult<ReturnType>>> ViewAsync<ReturnType>(string designDocName, string viewName, ViewOptions options = null)
        {
            var request = PrepareRequest($"_design/{designDocName}/_view/{viewName}");

            if (options != null)
            {
                request.Url.QueryParams.AddRange(options.ToQueryParameters());
            }

            var result = await ExecuteRequestAsync<JToken>(request, HttpMethod.Get);

            return result.SelectToken("rows").ToObject<List<ViewResult<ReturnType>>>();
        }

        /// <summary>
        /// Creates the database associated with this client if it doesn't already exist.
        /// </summary>
        public async Task<CreateDatabaseResponse> CreateDatabaseAsync()
        {
            var request = PrepareRequest("");
            var send = request.SendAsync(HttpMethod.Put);
            var response = await send;
            var rawBody = await send.ReceiveString();

            if ((int)response.StatusCode == 412)
            {
                return new CreateDatabaseResponse()
                {
                    AlreadyExisted = true,
                    Ok = true
                };
            }

            CheckAndThrowIfError(request, response, rawBody);

            var output = JsonConvert.DeserializeObject<CreateDatabaseResponse>(rawBody);
            output.AlreadyExisted = false;

            return output;
        }

        /// <summary>
        /// Deletes the database associated with this client.
        /// </summary>
        public async Task<CouchResponse> DeleteDatabaseAsync()
        {
            var request = PrepareRequest("");
            var response = await ExecuteRequestAsync<CouchResponse>(request, HttpMethod.Delete);

            return response;
        }
    }
}
