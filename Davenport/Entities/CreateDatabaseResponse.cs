using Newtonsoft.Json;

namespace Davenport.Entities
{
    public class CreateDatabaseResponse : CouchResponse
    {
        /// <summary>
        /// Whether the database already existed.
        /// </summary>
        public bool AlreadyExisted { get; set; }
    }
}