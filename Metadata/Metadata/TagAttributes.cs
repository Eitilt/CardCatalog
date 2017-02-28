using System;
using System.Collections.Generic;
using System.Text;

namespace Metadata {
    /// <summary>
    /// Common format-agnostic attributes mapping to different fields
    /// depending on how each is expressed in the particular format.
    /// </summary>
    public abstract class TagAttributes {
        /// <summary>
        /// The display name of the file.
        /// </summary>
        public abstract string Name { get; }
    }
}
