using System;

namespace Metadata {
    /// <summary>
    /// Indicate that the assembly should be scanned for classes marked with
    /// <see cref="MetadataFormatAttribute"/> to automatically register.
    /// 
    /// This class cannot be inherited.
    /// </summary>
    /// <seealso cref="MetadataFormatAttribute"/>
    /// <seealso cref="MetadataFormat.Register(System.Reflection.Assembly)"/>
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
    public sealed class MetadataFormatAssemblyAttribute : Attribute { }

    /// <summary>
    /// Indicate that the class implements a metadata format specification.
    /// 
    /// This class cannot be inherited.
    /// </summary>
    /// <remarks>
    /// The class must implement <see cref="ITagFormat"/>.
    /// </remarks>
    /// <seealso cref="MetadataFormatAssemblyAttribute"/>
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
    /// <remarks>
    /// Note that this function must be able to accept only a
    /// <see cref="System.IO.Stream"/> and return a <see cref="bool"/>,
    /// while leaving <see cref="System.IO.Stream.Position"/> with the same
    /// (potentially non-0) value as it had before the function was called.
    /// 
    /// This may change according to the TODO within
    /// <see cref="MetadataFormat.Parse(System.IO.Stream)"/>, likely by
    /// adding a `HeaderLength` parameter to this attribute.
    /// </remarks>
    /// TODO: Add discussion of required signature according to exceptions in
    /// method Register(...)
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class MetadataFormatValidatorAttribute : Attribute { }
}
