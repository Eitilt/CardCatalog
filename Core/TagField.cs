/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AgEitilt.Common.Stream.Extensions;

namespace AgEitilt.CardCatalog {
	/// <summary>
	/// Common format-agnostic attributes mapping to different fields
	/// depending on how each is expressed in the particular format.
	/// </summary>
	public interface ITagAttributes {
		/// <summary>
		/// The display names of the enclosing file.
		/// </summary>
		/// 
		/// TODO: This should only be a single value, not a list.
		IEnumerable<string> Name { get; }
	}

	/// <summary>
	/// A single point of data saved in the tag, with default helper
	/// implementations.
	/// </summary>
	public abstract class TagField : IParsable {
		/// <summary>
		/// The raw data making up this field's header.
		/// </summary>
		/// 
		/// <remarks>
		/// This -- rather than just parameters derived from it -- is included
		/// for transparency.
		/// </remarks>
		/// 
		/// <seealso cref="Data"/>
		public virtual byte[] Header { get; protected set; } = null;

		/// <summary>
		/// The raw data contained by this field, including any that would not
		/// be displayed by <see cref="Values"/>.
		/// </summary>
		/// 
		/// <seealso cref="Header"/>
		/// <seealso cref="Values"/>
		/// <seealso cref="HasHiddenData"/>
		public virtual byte[] Data { get; protected set; } = null;

		/// <summary>
		/// The byte header used to internally identify the field.
		/// </summary>
		/// 
		/// <remarks>
		/// This will typically return a sub-array extracted from
		/// <see cref="Header"/>.
		/// </remarks>
		public abstract byte[] SystemName { get; }

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
			String.Format(Strings.Base.Field_DefaultName, System.Text.Encoding.UTF8.GetString(SystemName));

		/// <summary>
		/// Extra human-readable information describing the field, such as the
		/// "category" of a header with multiple realizations.
		/// </summary>
		/// 
		/// <remarks>
		/// The default implementation is to return `null`; if this is not
		/// suitable, the method should be overridden.
		/// </remarks>
		public virtual string Subtitle => null;

		/// <summary>
		/// The length in bytes of the data contained in the field (excluding
		/// the header).
		/// </summary>
		/// 
		/// <remarks>
		/// With its default implementation, this must be available before
		/// <see cref="Parse(Stream)"/> is called.
		/// </remarks>
		public virtual int Length { get; } = 0;

		/// <summary>
		/// All data contained by this field, in a human-readable format.
		/// </summary>
		/// 
		/// <remarks>
		/// A <c>null</c> value should be treated the same as an empty
		/// enumerable.
		/// <para/>
		/// These will typically be <see cref="byte"/> array or
		/// <see cref="string"/> instances, but some fields may return other
		/// types, such as <see cref="double"/>. Using C#'s <c>as</c> or
		/// <c>is</c> keywords is intended for handling.
		/// <para/>
		/// Images will be returned as an <see cref="ImageData"/> wrapper
		/// around the raw data; the render and display should be handled by
		/// the including program depending on platform requirements and
		/// capabilities.
		/// </remarks>
		/// 
		/// <seealso cref="Data"/>
		/// <see cref="HasHiddenData"/>
		public abstract IEnumerable<object> Values { get; }

		/// <summary>
		/// Indicates whether this field includes data not displayed by
		/// <see cref="Values"/>.
		/// </summary>
		public virtual bool HasHiddenData { get; } = false;

		/// <summary>
		/// Read a sequence of bytes in the manner appropriate to the specific
		/// type of field.
		/// </summary>
		/// 
		/// <param name="stream">The data to read.</param>
		public virtual void Parse(Stream stream) {
			var data = new byte[Length];

			var read = stream.ReadAll(data, 0, Length);
			if (read < Length) {
				data = data.Take(read).ToArray();
			}

			Data = data;
		}

		/// <summary>
		/// A default implementation of <see cref="TagField"/>, not providing
		/// any data formatting.
		/// </summary>
		/// 
		/// <remarks>
		/// TODO: Allow more sophisticated Header/Data handling at Parse time.
		/// </remarks>
		public class Empty : TagField {
			/// <summary>
			/// The minimal constructor for creating a skeleton instance.
			/// </summary>
			/// 
			/// <param name="header">
			/// The full binary header of the unrecognized field.
			/// </param>
			/// <param name="name">
			/// The value to save to <see cref="SystemName"/>.
			/// </param>
			/// <param name="length">
			/// The value to save to <see cref="TagField.Length"/>.
			/// </param>
			public Empty(byte[] header, byte[] name, int length) {
				Header = header;
				this.name = name;
				this.length = length;
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
			/// Underlying field ID to work around the lack of a
			/// <see cref="Length"/>.set.
			/// </summary>
			private int length;
			/// <summary>
			/// The length in bytes of the data contained in the field (excluding
			/// the header).
			/// </summary>
			public override int Length => length;

			/// <summary>
			/// All data contained by this field, in a human-readable format.
			/// </summary>
			public override IEnumerable<object> Values =>
				new object[1] { Data };
		}
	}
}
