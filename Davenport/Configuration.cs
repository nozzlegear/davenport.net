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
    }
}
