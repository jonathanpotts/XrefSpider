using YamlDotNet.Serialization;

namespace XrefSpider.Models
{
    public class Xref
    {
        [YamlMember(Alias = "uid")]
        public string UniqueId { get; set; }

        [YamlMember(Alias = "name")]
        public string Name { get; set; }

        [YamlMember(Alias = "href")]
        public string Url { get; set; }

        [YamlMember(Alias = "commentId")]
        public string CommentId { get; set; }

        [YamlMember(Alias = "fullName")]
        public string FullName { get; set; }

        [YamlMember(Alias = "nameWithType")]
        public string NameWithType { get; set; }
    }
}
