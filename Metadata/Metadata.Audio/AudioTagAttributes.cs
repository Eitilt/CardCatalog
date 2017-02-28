using System;
using System.Collections.Generic;
using System.Text;

namespace Metadata.Audio {
    /// <summary>
    /// Common format-agnostic attributes specific to audio encoding, mapping
    /// to different fields depending on the particular metadata format.
    /// </summary>
    /// 
    /// <seealso cref="ITagField"/>
    public abstract class AudioTagAttributes : ITagAttributes {
        /// <summary>
        /// The display name of the enclosing file.
        /// </summary>
        public abstract string Name { get; }
    }
}
