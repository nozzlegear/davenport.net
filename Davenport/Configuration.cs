using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Davenport.Entities;
using Davenport.Infrastructure;
using Flurl.Http;
using Newtonsoft.Json;

namespace Davenport
{
    /// <summary>
    /// Configuration settings for Davenport clients.
    /// </summary>
    public class Configuration
    {
        /// <param name="couchUrl">URL string pointing to your CouchDB installation, e.g. http://localhost:5984. Should not include your database name.</param>
        /// <param name="databaseName">The name of your database. Should be URL compatible.</param>
        public Configuration(string couchUrl, string databaseName)
        {
            if (!Uri.IsWellFormedUriString(couchUrl, UriKind.Absolute))
            {
                throw new ArgumentException($"{nameof(couchUrl)} is not a well formed uri string.");
            }

            if (string.IsNullOrEmpty(databaseName))
            {
                throw new ArgumentNullException(nameof(databaseName));
            }

            CouchUrl = couchUrl;
            DatabaseName = databaseName;
        }

        /// <summary>
        /// URL string pointing to your CouchDB installation, e.g. http://localhost:5984. Should not include your database name.
        /// </summary>
        public string CouchUrl { get; set; }

        /// <summary>
        /// The name of your database. Should be URL compatible.
        /// </summary>
        public string DatabaseName { get; set; }

        /// <summary>
        /// Username that will be used for basic authentication. Optional, will be ignored if <see cref="Password"/> is empty.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Password that will be used for basic authentication. Optional, will be ignored if <see cref="Username"/> is empty.
        /// </summary>
        public string Password { get; set; }

        /// A custom JsonConverter which will be used when converting to and from json.
        public JsonConverter Converter { get; set; }

        /// <summary>
        /// A warning event fired when CouchDB returns a warning with its response, or when Davenport detects a possible error.
        /// </summary>
        public event EventHandler<string> Warning;

        public static async Task<string> GetVersionAsync(Configuration config)
        {
            var request = Flurl.Url.Combine(config.CouchUrl).AllowAnyHttpStatus();
            var response = await request.GetAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to connect to CouchDB instance at {config.DatabaseName}. {response.StatusCode} {response.ReasonPhrase}");
            }

            var infoBody = JsonConvert.DeserializeAnonymousType(await response.Content.ReadAsStringAsync(), new { version = "" });

            return infoBody.version;
        }

        public static async Task<bool> IsVersion2OrAboveAsync(Configuration config)
        {
            var versionStr = await GetVersionAsync(config);

            return IsVersion2OrAbove(versionStr);
        }

        public static bool IsVersion2OrAbove(string versionStr)
        {
            var version = Convert.ToInt32(versionStr.Split('.')[0]);

            return version >= 2;
        }

        /// Creates a CouchDB database if it doesn't exist.
        public static async Task CreateDatabaseAsync(Configuration config)
        {
            var client = Flurl.Url.Combine(config.CouchUrl, config.DatabaseName).AllowAnyHttpStatus();
            var request = client.PutAsync(null);
            var response = await request;

            // 429 Precondition Failed: Database already exists.
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.PreconditionFailed)
            {
                var body = await request.ReceiveString();

                throw new DavenportException($"Failed to create database {config.DatabaseName}.")
                {
                    StatusCode = (int)response.StatusCode,
                    StatusText = response.ReasonPhrase,
                    Url = client.Url.ToString(),
                    ResponseBody = body,
                };
            }
        }

        /// Creates indexes for the given fields. This makes querying with the Find methods and selectors faster.
        /// Will throw an ArgumentException if no indexes are given.
        public static async Task CreateDatabaseIndexesAsync(Configuration config, IEnumerable<string> indexes)
        {
            if (indexes.Count() == 0)
            {
                throw new ArgumentException($"{nameof(indexes)} contains 0 values.");
            }

            var client = Flurl.Url.Combine(config.CouchUrl, config.DatabaseName, "_index").AllowAnyHttpStatus();
            var indexData = new
            {
                fields = indexes
            };
            var content = new JsonContent(new
            {
                index = indexData,
                name = $"{config.DatabaseName}-indexes"
            });
            var request = client.PostAsync(content);
            var response = await request;

            if (!response.IsSuccessStatusCode)
            {
                throw new DavenportException($"Error creating CouchDB indexes on database ${config.DatabaseName}.")
                {
                    StatusCode = (int)response.StatusCode,
                    StatusText = response.ReasonPhrase,
                    Url = client.Url.ToString(),
                    ResponseBody = await request.ReceiveString(),
                };
            }
        }

        /// Creates the given design docs. Will check that each view in each design doc has functions that perfectly match the ones found in the database, and update them if they don't match.
        /// Will throw an ArgumentException if no design docs are given.
        public static async Task CreateDesignDocsAsync(Configuration config, IEnumerable<DesignDocConfig> designDocs = null)
        {
            foreach (var docConfiguration in designDocs)
            {
                var client = Flurl.Url.Combine(config.CouchUrl, config.DatabaseName, "_design", docConfiguration.Name).AllowAnyHttpStatus();
                var getRequest = client.GetAsync();
                var getResult = await getRequest;
                string body = await getRequest.ReceiveString();

                // If CouchDB returns a 404, we'll need to create the design document.
                if (!getResult.IsSuccessStatusCode && getResult.StatusCode != HttpStatusCode.NotFound)
                {
                    throw new DavenportException($"Davenport: Failed to retrieve design doc \"{docConfiguration.Name}\". {getResult.StatusCode} {getResult.ReasonPhrase}")
                    {
                        StatusCode = (int)getResult.StatusCode,
                        StatusText = getResult.ReasonPhrase,
                        Url = client.Url.ToString(),
                        ResponseBody = body,
                    };
                }

                DesignDoc docFromDatabase;

                if (!getResult.IsSuccessStatusCode)
                {
                    docFromDatabase = new DesignDoc()
                    {
                        Id = $"_design/{docConfiguration.Name}",
                        Language = "javascript",
                    };
                }
                else
                {
                    docFromDatabase = JsonConvert.DeserializeObject<DesignDoc>(body);
                }

                var databaseViews = docFromDatabase.Views ?? new Dictionary<string, View>();
                var shouldUpdate = false;

                foreach (var view in docConfiguration.Views)
                {
                    if (!databaseViews.ContainsKey(view.Name))
                    {
                        databaseViews.Add(view.Name, view);

                        shouldUpdate = true;

                        continue;
                    }

                    var dbView = databaseViews[view.Name];

                    if (dbView.MapFunction != view.MapFunction || dbView.ReduceFunction != view.ReduceFunction)
                    {
                        databaseViews.Remove(view.Name);
                        databaseViews.Add(view.Name, view);

                        shouldUpdate = true;
                    }
                }

                if (shouldUpdate)
                {
                    config.InvokeWarningEvent(config, $"Creating or updating design doc {docConfiguration.Name}.");

                    docFromDatabase.Views = databaseViews;

                    var request = client.PutAsync(new JsonContent(docFromDatabase));
                    var response = await request;
                    var responseBody = await request.ReceiveString();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new DavenportException($"Could not create or update CouchDB design doc \"{docConfiguration.Name}\". {response.StatusCode} {response.ReasonPhrase}")
                        {
                            StatusCode = (int)response.StatusCode,
                            StatusText = response.ReasonPhrase,
                            Url = client.Url.ToString(),
                            ResponseBody = responseBody,
                        };
                    }
                }
            }
        }

        /// Configures a Davenport client and database by validating the CouchDB version, creating indexes and design documents, and then returning a client to interact with the database.
        public static async Task<Client<DocumentType>> ConfigureDatabaseAsync<DocumentType>(Configuration config, IEnumerable<string> indexes = null, IEnumerable<DesignDocConfig> designDocs = null) where DocumentType : CouchDoc
        {
            var version = await GetVersionAsync(config);

            if (!IsVersion2OrAbove(version))
            {
                config.InvokeWarningEvent(config, $"Warning: Davenport expects your CouchDB instance to be running CouchDB 2.0 or higher. Version detected: {version}. Some database methods may not work.");
            }

            await CreateDatabaseAsync(config);

            if (indexes != null && indexes.Count() > 0)
            {
                await CreateDatabaseIndexesAsync(config, indexes);
            }

            if (designDocs != null && designDocs.Count() > 0)
            {
                await CreateDesignDocsAsync(config, designDocs);
            }

            return new Client<DocumentType>(config);
        }

        internal void InvokeWarningEvent(object sender, string message)
        {
            Warning?.Invoke(sender, message);
        }
    }
}
