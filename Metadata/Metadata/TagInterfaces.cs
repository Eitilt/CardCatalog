using System.Collections.Generic;

namespace Metadata {
	/// <summary>
	/// Common format-agnostic attributes mapping to different fields
	/// depending on how each is expressed in the particular format.
	/// </summary>
	public interface ITagAttributes {
		/// <summary>
		/// The display names of the enclosing file.
		/// </summary>
		IEnumerable<string> Name { get; }
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
		/// The length in bytes of the data contained in the field (excluding
		/// the header).
		/// </summary>
		public uint Length { get; protected set; }

		/// <summary>
		/// The human-readable name of the field if available, or a
		/// representation of <see cref="SystemName"/> if not.
		/// </summary>
		/// 
		/// <remarks>
		/// The default implementation is to read the <see cref="SystemName"/>
		/// as a UTF-8 encoded string enclosed in "{ " and " }"; if this is
		/// not suitable, the method should be overridden.
		/// </remarks>
		public virtual string Name =>
			System.String.Format("{{ {0} }}", System.Text.Encoding.UTF8.GetString(SystemName));

		/// <summary>
		/// All values contained within this field.
		/// </summary>
		public abstract IEnumerable<string> Values { get; }

		/// <summary>
		/// Read a sequence of bytes in the manner appropriate to the specific
		/// type of field.
		/// </summary>
		/// 
		/// <param name="data">The data to read.</param>
		protected internal abstract void Parse(IEnumerable<byte> data);
	}
}
