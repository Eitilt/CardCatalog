using System;

namespace Metadata {
	/// <summary>
	/// Indicate that the assembly should be scanned for classes marked with
	/// <see cref="MetadataFormatAttribute"/> to automatically register.
	/// 
	/// This class cannot be inherited.
	/// </summary>
	/// 
	/// <seealso cref="MetadataFormatAttribute"/>
	/// <seealso cref="MetadataFormat.Register(System.Reflection.Assembly)"/>
	[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
	public sealed class ScanAssemblyAttribute : Attribute { }

	/// <summary>
	/// Indicate that the class implements a metadata format specification.
	/// 
	/// This class cannot be inherited.
	/// </summary>
	/// 
	/// <remarks>
	/// The class must implement <see cref="TagFormat"/>.
	/// </remarks>
	/// 
	/// <seealso cref="ScanAssemblyAttribute"/>
	/// <seealso cref="MetadataFormat.Register(string, Type)"/>
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class MetadataFormatAttribute : Attribute {
		/// <summary>
		/// The unique short name representing this format.
		/// </summary>
		public string Name { get; private set; }

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
	/// <seealso cref="MetadataFormat.Register(string, int, System.Reflection.MethodInfo)"/>
	[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
	public sealed class HeaderParserAttribute : Attribute {
		int length;
		/// <summary>
		/// The number of bytes this function processes to verify that the
		/// proper format header is present.
		/// </summary>
		/// 
		/// <exception cref="ArgumentOutOfRangeException">
		/// New value for `set` is less than one.
		/// </exception>
		public int HeaderLength {
			get => length;
			set {
				if (value <= 0)
					throw new ArgumentOutOfRangeException("Metadata format headers must be at least one byte long");

				length = value;
			}
		}

		/// <summary>
		/// Initializes a new instance of the
		/// <see cref="HeaderParserAttribute"/> class with the
		/// specified <see cref="HeaderLength"/>.
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
		public HeaderParserAttribute(int length) {
			HeaderLength = length;
		}
	}

	/// <summary>
	/// Marks a class as describing a field within a tag in a particular metadata
	/// format.
	/// </summary>
	/// 
	/// <remarks>
	/// An exception will be thrown on registration if the class does not
	/// implement <see cref="TagField"/>.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
	public sealed class TagFieldAttribute : Attribute {
		/// <summary>
		/// The short name associated with the metadata format, or `null` if
		/// it should be obtained from the enclosing type.
		/// </summary>
		/// 
		/// <remarks>
		/// If this is `null` (the default), the immediately-enclosing type
		/// <em>must</em>  have a <see cref="MetadataFormatAttribute"/> attribute,
		/// or an  exception will be thrown on the call to
		/// <see cref="MetadataFormat.Register(string, byte[], Type)"/>.
		/// </remarks>
		/// 
		/// <seealso cref="MetadataFormatAttribute.Name"/>
		public string Format { get; set; } = null;

		/// <summary>
		/// A sequence of bytes used to uniquely identify this type of field.
		/// </summary>
		/// 
		/// <remarks>
		/// If multiple fields have the same structure and differ only in this
		/// identifier, the <see cref="TagFieldAttribute"/> may be added to a
		/// common class once for each such tag.
		/// </remarks>
		public byte[] Header { get; private set; }

		/// <summary>
		/// Initializes a new instance of the
		/// <see cref="TagFieldAttribute"/> class with the
		/// specified <see cref="Header"/>.
		/// </summary>
		/// 
		/// <param name="header">
		/// The unique byte header indicating this type of field.
		/// </param>
		public TagFieldAttribute(byte[] header) {
			Header = header;
		}
	}
}
