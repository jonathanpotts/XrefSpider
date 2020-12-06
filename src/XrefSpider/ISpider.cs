using System.Threading.Tasks;

namespace XrefSpider
{
    /// <summary>
    /// Interface that spiders must implement.
    /// </summary>
    public interface ISpider
    {
        /// <summary>
        /// Crawls the documentation and creates the xref map.
        /// </summary>
        /// <returns>Task that crawls the documentation.</returns>
        public Task CrawlAsync();
    }
}
