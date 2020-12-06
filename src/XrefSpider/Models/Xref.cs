using YamlDotNet.Serialization;

namespace XrefSpider.Models
{
    /// <summary>
    /// Xref (cross-reference) metadata.
    /// </summary>
    public class Xref
    {
        /// <summary>
        /// Unique identifier.
        /// </summary>
        [YamlMember(Alias = "uid")]
        public string UniqueId { get; set; }

        /// <summary>
        /// Name.
        /// </summary>
        [YamlMember(Alias = "name")]
        public string Name { get; set; }

        /// <summary>
        /// URL to documentation page.
        /// </summary>
        [YamlMember(Alias = "href")]
        public string Url { get; set; }

        /// <summary>
        /// Comment identifier.
        /// </summary>
        [YamlMember(Alias = "commentId")]
        public string CommentId { get; set; }

        /// <summary>
        /// Full name.
        /// </summary>
        [YamlMember(Alias = "fullName")]
        public string FullName { get; set; }

        /// <summary>
        /// Name with type.
        /// </summary>
        [YamlMember(Alias = "nameWithType")]
        public string NameWithType { get; set; }
    }
}
