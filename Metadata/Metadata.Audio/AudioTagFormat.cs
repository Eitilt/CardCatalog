using System;
using System.IO;

[assembly: Metadata.MetadataFormatAssembly]

namespace Metadata.Audio {
    /// <summary>
    /// Common properties to retrieve info from multiple audio formats.
    /// </summary>
    public abstract class AudioTagFormat : TagFormat {
        /// <summary>
        /// The proper standardized field redirects for the enclosing
        /// metadata format.
        /// </summary>
        /// 
        /// <seealso cref="TagFormat.Fields"/>
        /// <seealso cref="AudioAttributes"/>
        public sealed override ITagAttributes Attributes => (ITagAttributes)AudioAttributes;

        /// <summary>
        /// The standard field redirects extended with attributes specific to
        /// audio metadata.
        /// </summary>
        /// 
        /// <seealso cref="TagFormat.Fields"/>
        /// <seealso cref="Attributes"/>
        public abstract AudioTagAttributes AudioAttributes { get; }

        /// <summary>
        /// Temporary implementation to allow building.
        /// </summary>
        /// 
        /// <param name="stream">The stream to parse.</param>
        public override void Parse(BinaryReader stream) { }
    }
}
