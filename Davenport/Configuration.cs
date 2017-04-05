using System;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Newtonsoft.Json;

namespace Davenport
{
    /// <summary>
    /// Configuration settings for Davenport clients.
    /// </summary>
    public class Configuration
    {
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

        /// <summary>
        /// A warning event fired when CouchDB returns a warning with its response, or when Davenport detects a possible error.
        /// </summary>
        public event EventHandler<string> Warning;

        /// <summary>
        /// Configures a Davenport client and database by validating the CouchDB version, creating indexes and design documents, and then returning a client to interact with the database.
        /// </summary>
        public static async Task ConfigureDatabase(Configuration config)
        {
            // Connect to the database and parse its version
            using (var client = Flurl.Url.Combine(config.CouchUrl).AllowAnyHttpStatus())
            {
                var request = client.GetAsync();
                var response = await request;
                
                if (! response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to connect to CouchDB instance at {config.DatabaseName}. {response.StatusCode} {response.ReasonPhrase}");
                }

                var infoBody = JsonConvert.DeserializeAnonymousType( await request.ReceiveString(), new { version = "" });
                var version = Convert.ToDecimal(infoBody);

                if (version < 2)
                {
                    config.Warning?.Invoke(config, $"Warning: Davenport expects your CouchDB instance to be running CouchDB 2.0 or higher. Version detected: {infoBody.version}. Some database methods may not work.");
                }
            }
        }
    }
}
