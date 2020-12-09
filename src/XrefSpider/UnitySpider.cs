using System;
using System.Threading.Tasks;

namespace XrefSpider
{
    /// <summary>
    /// Spider that crawls the Unity scripting reference.
    /// </summary>
    public class UnitySpider : ISpider
    {
        /// <summary>
        /// Crawls the documentation and creates the xref map.
        /// </summary>
        /// <returns>Task that crawls the documentation.</returns>
        public Task<string> CrawlAsync()
        {
            throw new NotImplementedException();
        }
    }
}
