[assembly: Metadata.MetadataFormatAssembly]

namespace Metadata.Audio {
    /// <summary>
    /// Common properties to retrieve info from multiple audio formats.
    /// </summary>
    public abstract class AudioTagFormat : MetadataFormat.ITagFormat {
        /// <summary>
        /// Redirect to allow the more specific attribute format to satisfy
        /// the interface implementation.
        /// </summary>
        TagAttributes MetadataFormat.ITagFormat.Attributes => Attributes;
        /// <summary>
        /// The proper standardized field redirects for the enclosing
        /// audio metadata format.
        /// </summary>
        public abstract AudioTagAttributes Attributes { get; }
    }
}
