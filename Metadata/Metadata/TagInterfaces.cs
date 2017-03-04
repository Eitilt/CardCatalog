using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Metadata {
	/// <summary>
	/// Common properties to retrieve info from multiple tag formats.
	/// </summary>
	public abstract class TagFormat {
		/// <summary>
		/// The display name of the tag format.
		/// </summary>
		public abstract string Format { get; }

		/// <summary>
		/// The length in bytes of the tag, not including the header.
		/// </summary>
		/// 
		/// <remarks>
		/// The underlying value should be set in any function marked with
		/// <see cref="HeaderParserAttribute"/>; if that function
		/// sets it to 0, the incoming stream will be read according to
		/// </remarks>
		public uint Length { get; protected set; }

		/// <summary>
		/// The low-level representations of the tag data.
		/// </summary>
		public abstract IReadOnlyFieldDictionary Fields { get; }

		/// <summary>
		/// The proper standardized field redirects for the enclosing
		/// metadata format.
		/// </summary>
		/// 
		/// <seealso cref="Fields"/>
		public abstract ITagAttributes Attributes { get; }

		/// <summary>
		/// Parse a tag of unknown length until some end-of-tag marker is
		/// reached.
		/// </summary>
		/// 
		/// <param name="stream">The stream to read.</param>
		public abstract void Parse(BinaryReader stream);
		/// <summary>
		/// Parse a tag of known length asynchronously.
		/// </summary>
		/// 
		/// <remarks>
		/// The default implementation simply redirects the data to
		/// <see cref="Parse(BinaryReader)"/> within a <see cref="Task"/>.
		/// </remarks>
		/// 
		/// <param name="data">The sequence of bytes to parse.</param>
		public virtual Task ParseAsync(IEnumerable<byte> data) {
			using (var stream = new MemoryStream(data.ToArray()))
			using (var reader = new BinaryReader(stream))
				return Task.Run(() => Parse(reader));
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
	}

	/// <summary>
	/// Common format-agnostic attributes mapping to different fields
	/// depending on how each is expressed in the particular format.
	/// </summary>
	public interface ITagAttributes {
		/// <summary>
		/// The display names of the enclosing file.
		/// </summary>
		TagFormat.AttributeValues Name { get; }
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
