using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XrefService.Models
{
    /// <summary>
    /// Xref (cross-reference) metadata.
    /// </summary>
    public class Xref
    {
        /// <summary>
        /// Unique identifier.
        /// </summary>
        [JsonPropertyName("uid")]
        public string UniqueId { get; set; }

        /// <summary>
        /// Name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Full name.
        /// </summary>
        [JsonPropertyName("fullName")]
        public string FullName { get; set; }

        /// <summary>
        /// Hypertext reference (URL of reference page).
        /// </summary>
        [JsonPropertyName("href")]
        public string HypertextReference { get; set; }

        /// <summary>
        /// Schema type.
        /// </summary>
        [JsonPropertyName("schemaType")]
        public string SchemaType { get; set; }

        /// <summary>
        /// Comment identifier.
        /// </summary>
        [JsonPropertyName("commentId")]
        public string CommentId { get; set; }

        /// <summary>
        /// Monikers.
        /// </summary>
        [JsonPropertyName("monikers")]
        public IList<string> Monikers { get; set; }

        /// <summary>
        /// Name with type.
        /// </summary>
        [JsonPropertyName("nameWithType")]
        public string NameWithType { get; set; }

        /// <summary>
        /// Summary HTML.
        /// </summary>
        [JsonPropertyName("summary")]
        public string SummaryHtml { get; set; }

        /// <summary>
        /// Tags.
        /// </summary>
        [JsonPropertyName("tags")]
        public IList<string> Tags { get; set; }
    }
}
