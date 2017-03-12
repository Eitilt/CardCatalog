using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Metadata {
	/// <summary>
	/// Common properties to retrieve info from multiple tag formats.
	/// </summary>
	public abstract class MetadataTag : IParsable {
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
		public int Length { get; protected set; }

		/// <summary>
		/// An editable redirect for the low-level data.
		/// </summary>
		/// 
		/// <seealso cref="Fields"/>
		protected abstract FieldDictionary FieldBase { get; set; }

		/// <summary>
		/// The low-level representations of the tag data.
		/// </summary>
		public IReadOnlyFieldDictionary Fields => FieldBase;

		/// <summary>
		/// The proper standardized field redirects for the enclosing
		/// metadata format.
		/// </summary>
		/// 
		/// <seealso cref="Fields"/>
		public abstract ITagAttributes Attributes { get; }

		/// <summary>
		/// Parse the fields contained within a tag.
		/// </summary>
		/// 
		/// <remarks>
		/// TODO: Handle tags with unknown length.
		/// </remarks>
		/// 
		/// <param name="stream">The stream to read.</param>
		public void Parse(Stream stream) {
			var fields = ReflectionData<TagField>.ParseAsync(stream, MetadataFormat.FormatFields(Format)).Result;

			FieldBase = new FieldDictionary(fields.GroupBy(f => f.SystemName).ToDictionary((g => g.Key), (g => g.ToList() as IEnumerable<TagField>)));
		}
	}
}
