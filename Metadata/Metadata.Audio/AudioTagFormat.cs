[assembly: Metadata.MetadataFormatAssembly]

namespace Metadata.Audio {
    /// <summary>
    /// Common properties to retrieve info from multiple audio formats.
    /// </summary>
    public abstract class AudioTagFormat : ITagFormat {
        /// <summary>
        /// The display name of the tag format.
        /// </summary>
        public abstract string Format { get; }

        /// <summary>
        /// The low-level representations of the tag data.
        /// </summary>
        public abstract System.Collections.Generic.IReadOnlyDictionary<byte[], ITagField> Fields { get; }

        /// <summary>
        /// Redirect to allow the more specific attribute format to satisfy
        /// the interface implementation.
        /// </summary>
        ITagAttributes ITagFormat.Attributes => Attributes;
        /// <summary>
        /// The proper standardized field redirects for the enclosing
        /// audio metadata format.
        /// </summary>
        /// 
        /// <seealso cref="Fields"/>
        public abstract AudioTagAttributes Attributes { get; }
    }
}
