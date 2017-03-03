using System.Collections.Generic;

namespace Metadata {
    /// <summary>
    /// Common properties to retrieve info from multiple tag formats.
    /// </summary>
    public interface ITagFormat {
        /// <summary>
        /// The display name of the tag format.
        /// </summary>
        string Format { get; }

        /// <summary>
        /// The low-level representations of the tag data.
        /// </summary>
        IReadOnlyFieldDictionary Fields { get; }

        /// <summary>
        /// The proper standardized field redirects for the enclosing
        /// metadata format.
        /// </summary>
        /// 
        /// <seealso cref="Fields"/>
        ITagAttributes Attributes { get; }
    }

    /// <summary>
    /// Provide more obvious semantics for the values of standard fields.
    /// </summary>
    /// 
    /// <remarks>
    /// Many metadata formats support multiple instances of the same field,
    /// and making this a list rather than a single string also unifies
    /// behaviour between individual tags and files containing multiple tags.
    /// </remarks>
    public class AttributeValues : List<string> { }

    /// <summary>
    /// Common format-agnostic attributes mapping to different fields
    /// depending on how each is expressed in the particular format.
    /// </summary>
    public interface ITagAttributes {
        /// <summary>
        /// The display names of the enclosing file.
        /// </summary>
        AttributeValues Name { get; }
    }

    /// <summary>
    /// A single point of data saved in the tag, with default helper
    /// implementations.
    /// </summary>
    public abstract class TagField {
        /// <summary>
        /// The byte header used to internally identify the field.
        /// </summary>
        public abstract byte[] SystemName { get; }

        /// <summary>
        /// The human-readable name of the field if available, or a
        /// representation of <see cref="SystemName"/> if not.
        /// </summary>
        /// <remarks>
        /// The default implementation is to read the <see cref="SystemName"/>
        /// as a UTF-8 encoded string enclosed in "{ " and " }"; if this is
        /// not suitable, the method should be overridden.
        /// </remarks>
        public virtual string Name =>
            System.String.Format("{{ {0} }}", System.Text.Encoding.UTF8.GetString(SystemName));
    }
}
