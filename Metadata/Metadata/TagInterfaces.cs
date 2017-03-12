using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
	public abstract class TagField : IParsable {
		/// <summary>
		/// A default implementation of <see cref="TagField"/>, not providing
		/// any data formatting.
		/// </summary>
		public class Empty : TagField {
			/// <summary>
			/// The minimal constructor for creating a skeleton instance.
			/// </summary>
			/// 
			/// <param name="name">
			/// The value to save to <see cref="SystemName"/>.
			/// </param>
			/// <param name="length">
			/// The value to save to <see cref="TagField.Length"/>.
			/// </param>
			public Empty(byte[] name, int length) {
				this.name = name;
				
				Length = length;
				data = new byte[length];
			}

			/// <summary>
			/// Underlying field ID to work around the lack of a
			/// <see cref="SystemName"/>.set.
			/// </summary>
			private byte[] name;
			/// <summary>
			/// The byte header used to internally identify the field.
			/// </summary>
			public override byte[] SystemName => name;

			/// <summary>
			/// The container to hold the raw data of this field.
			/// </summary>
			private byte[] data;
			/// <summary>
			/// All data contained by this field, in a human-readable format.
			/// </summary>
			public override IEnumerable<string> Values => new string[1] { BitConverter.ToString(data).Replace('-', ' ') };

			/// <summary>
			/// Read a sequence of bytes in the manner appropriate to the
			/// specific type of field.
			/// </summary>
			/// 
			/// <param name="stream">The data to read.</param>
			public override void Parse(Stream stream) {
				var read = stream.ReadAll(data, 0, Length);
				if (read < Length) {
					Length = read;
					data = data.Take(read).ToArray();
				}
			}
		}

		/// <summary>
		/// The byte header used to internally identify the field.
		/// </summary>
		public abstract byte[] SystemName { get; }
		
		/// <summary>
		/// The length in bytes of the data contained in the field (excluding
		/// the header).
		/// </summary>
		public int Length { get; protected set; }

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
		/// All data contained by this field, in a human-readable format.
		/// </summary>
		public abstract IEnumerable<string> Values { get; }

		/// <summary>
		/// Read a sequence of bytes in the manner appropriate to the specific
		/// type of field.
		/// </summary>
		/// 
		/// <param name="stream">The data to read.</param>
		public abstract void Parse(Stream stream);
	}
}
