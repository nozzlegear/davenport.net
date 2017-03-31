namespace Davenport.Interfaces
{
    public interface ICouchDoc
    {
        /// <summary>
        /// The object's database id.
        /// </summary>
        string _id { get; set; }
            
        /// <summary>
        /// The object's database revision.
        /// </summary>
        string _rev { get; set; }
    }
}
