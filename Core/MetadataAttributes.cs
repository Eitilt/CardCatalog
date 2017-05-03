/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Text;

namespace AgEitilt.CardCatalog {
	/// <summary>
	/// Indicate that the class implements a metadata format specification.
	/// 
	/// This class cannot be inherited.
	/// </summary>
	/// 
	/// <remarks>
	/// The class must implement <see cref="MetadataTag"/>.
	/// </remarks>
	/// 
	/// <seealso cref="FormatRegistry.Register{T}(string)"/>
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
	public sealed class MetadataFormatAttribute : Attribute {
		/// <summary>
		/// The unique short name representing this format.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Initializes a new instance of the
		/// <see cref="MetadataFormatAttribute"/> class with the specified
		/// <see cref="Name"/>.
		/// </summary>
		/// 
		/// <param name="name">
		/// The unique short name representing this format.
		/// <para/>
		/// It is recommended that this also be exposed as a constant on the
		/// type itself.
		/// </param>
		public MetadataFormatAttribute(string name) {
			Name = name;
		}
	}

	/// <summary>
	/// Marks a method as being a validation function checking a binary header
	/// against that defined by the enclosing metadata specification.
	/// 
	/// This class cannot be inherited.
	/// </summary>
	/// 
	/// <remarks>
	/// If the passed byte[] is not a header of the proper format, the
	/// function should return `null`.
	/// </remarks>
	/// 
	/// TODO: Add discussion of required signature according to exceptions in
	/// method Register(...)
	/// 
	/// <seealso cref="FormatRegistry.Register(string, uint, System.Reflection.MethodInfo)"/>
	[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
	public sealed class HeaderParserAttribute : Attribute {
		/// <summary>
		/// The number of bytes this function processes to verify that the
		/// proper format header is present.
		/// </summary>
		/// 
		/// <exception cref="ArgumentOutOfRangeException">
		/// New value for `set` is less than one.
		/// </exception>
		public uint HeaderLength { get; }

		/// <summary>
		/// Initializes a new instance of the
		/// <see cref="HeaderParserAttribute"/> class with the specified
		/// <see cref="HeaderLength"/>.
		/// </summary>
		/// 
		/// <param name="length">
		/// The number of bytes this function processes to verify that the
		/// proper format header is present.
		/// </param>
		/// 
		/// <exception cref="ArgumentOutOfRangeException">
		/// Value of <paramref name="length"/> is less than one.
		/// </exception>
		public HeaderParserAttribute(uint length) {
			HeaderLength = length;
		}
	}

	/// <summary>
	/// Marks a class as describing a field within a tag in a particular
	/// metadata format.
	/// </summary>
	/// 
	/// <remarks>
	/// An exception will be thrown on registration if the class does not
	/// implement <see cref="TagField"/>.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
	public sealed class TagFieldAttribute : Attribute {
		/// <summary>
		/// Reference to the ISO-8859-1 character encoding to allow passing
		/// byte headers as compile-time constant `string`s.
		/// </summary>
		static Encoding ISO88591 = Encoding.GetEncoding(28591);

		/// <summary>
		/// The short name associated with the metadata format, or `null` if
		/// it should be obtained from the enclosing type.
		/// </summary>
		/// 
		/// <remarks>
		/// If this is `null` (the default), the immediately-enclosing type
		/// <em>must</em>  have a <see cref="MetadataFormatAttribute"/>
		/// attribute, or an exception will be thrown on the call to
		/// <see cref="FormatRegistry.Register{T}(string, byte[])"/>.
		/// </remarks>
		/// 
		/// <seealso cref="MetadataFormatAttribute.Name"/>
		public string Format { get; } = null;

		/// <summary>
		/// A sequence of bytes used to uniquely identify this type of field.
		/// </summary>
		/// 
		/// <remarks>
		/// If multiple fields have the same structure and differ only in this
		/// identifier, the <see cref="TagFieldAttribute"/> may be added to a
		/// common class once for each such tag.
		/// </remarks>
		public byte[] Header { get; } = null;

		/// <summary>
		/// Initializes a new instance of the <see cref="TagFieldAttribute"/>
		/// class with headers as given by any members decorated with a
		/// <see cref="FieldNamesAttribute"/>.
		/// </summary>
		public TagFieldAttribute() { }

		/// <summary>
		/// Initializes a new instance of the <see cref="TagFieldAttribute"/>
		/// class with the specified <see cref="Header"/>.
		/// </summary>
		/// 
		/// <remarks>
		/// <paramref name="header"/> is passed as a `string` rather than a
		/// `byte[]` as it needs to be assigned via a compile-time constant.
		/// </remarks>
		/// 
		/// <param name="header">
		/// The unique byte header indicating this type of field, represented
		/// as an ISO-8859-1 string.
		/// </param>
		public TagFieldAttribute(string header) {
			Header = ISO88591.GetBytes(header);
		}
	}

	/// <summary>
	/// Marks a method as returning a list of the headers to which the
	/// enclosing class applies, or a parameter/property as being such a
	/// predefined list.
	/// </summary>
	/// 
	/// <remarks>
	/// The field will not be registered under the returned under the returned
	/// headers unless the class has a <see cref="TagFieldAttribute"/> without
	/// any string parameter.
	/// <para />
	/// If on a method: an exception will be thrown on class registration if
	/// it cannot be called without parameters or if it doesn't return an
	/// <see cref="T:IEnumerable{byte[]}"/>; if on a parameter or property, it
	/// simply needs to be of that type. In both cases, the member must be
	/// `static` and `public`.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
	public sealed class FieldNamesAttribute : Attribute { }
}
