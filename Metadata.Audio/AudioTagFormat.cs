using System;
using System.Collections.Generic;
using System.Text;

namespace Metadata.Audio {
    public abstract class AudioTagFormat : MetadataFormat.ITagFormat {
        TagAttributes MetadataFormat.ITagFormat.Attributes => Attributes;
        public abstract AudioTagAttributes Attributes { get; }
    }
}
