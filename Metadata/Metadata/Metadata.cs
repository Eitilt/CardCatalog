using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Metadata {
    /// <summary>
    /// A data-free class providing a common means to work with multiple
    /// metadata formats.
    /// </summary>
    public static class MetadataFormat {
        /// <summary>
        /// Validation functions for each registered metadata format.
        /// </summary>
        /// <seealso cref="Validate(string, Stream)"/>
        private static Dictionary<string, Func<Stream, bool>> tagValidationFunctions;
        /// <summary>
        /// Check whether the stream begins with metadata in the desired
        /// format.
        /// </summary>
        /// <param name="format">
        /// The short name of the metadata format.
        /// </param>
        /// <param name="stream">The Stream to test.</param>
        /// <returns>
        /// Whether the Stream begins with metadata in the desired format.
        /// </returns>
        /// <seealso cref="Parse(Stream)"/>
        public static bool Validate(string format, Stream stream) {
            return tagValidationFunctions[format](stream);
        }

        /// <summary>
        /// The class encapsulating each registered metadata format.
        /// </summary>
        /// <seealso cref="FormatType(string)"/>
        private static Dictionary<string, Type> tagFormats;
        /// <summary>
        /// Retrieve the class implementing the desired metadata format.
        /// </summary>
        /// <param name="format">
        /// The short name of the metadata format.
        /// </param>
        /// <returns>The class encapsulating the metadata format.</returns>
        /// <seealso cref="Construct(string, Stream)"/>
        public static Type FormatType(string format) {
            return tagFormats[format];
        }

        /// <summary>
        /// A registry of previously-scanned assemblies in order to prevent
        /// unnecessary use of reflection methods.
        /// </summary>
        private static HashSet<string> assemblies = new HashSet<string>();

        /// <summary>
        /// Initialize static attributes.
        /// </summary>
        /// <seealso cref="RefreshFormats"/>
        static MetadataFormat() {
            tagValidationFunctions = new Dictionary<string, Func<Stream, bool>>();
            tagFormats = new Dictionary<string, Type>();

            RefreshFormats();
        }
        /// <summary>
        /// Scan all currently-loaded assemblies for implementations of
        /// metadata formats.
        /// </summary>
        public static void RefreshFormats() {
            /*TODO: This is being provided through a Nuget package; once .NET
             * Standard 2.0 comes out, switch to using the builtin if possible
             * (also will give access to the CurrentDomain.AssemblyLoad event
             * to check for registration automatically)
             */
            /*TODO: Would probably be better to scan all referenced assemblies
             * (loaded or not) and load those that aren't yet, to avoid issues
             * with not recognizing a format when it would be expected. See
             * .NET Core's AssemblyLoadContext.
             */
            //BUG: GetAssemblies() returns an empty array
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                    .Where((a) => a.IsDefined(typeof(MetadataFormatAssemblyAttribute))))
                Register(assembly);
        }

        /// <summary>
        /// Scan the given assembly for all types marked as implementing a
        /// metadata format in a suitable manner for automatic lookup.
        /// <para/>
        /// Subsequent calls on a previously-scanned assembly will be ignored
        /// in order to save unnecessary type reflection.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        /// <seealso cref="MetadataFormatAttribute"/>
        public static void Register(Assembly assembly) {
            // Avoid searching assemblies multiple times to cut down on the
            // performance hit of reflection
            if (assemblies.Contains(assembly.FullName))
                return;

            foreach (Type t in assembly.ExportedTypes) {
                var attr = t.GetTypeInfo().GetCustomAttribute<MetadataFormatAttribute>(false);
                if (attr == null)
                    continue;

                Register(attr.Name, t);
            }

            assemblies.Add(assembly.FullName);
        }
        /// <summary>
        /// Add the given type to the lookup tables according to the descriptor
        /// specified in its <see cref="MetadataFormatAttribute.Name"/>.
        /// </summary>
        /// <remarks>
        /// Note that if multiple types are registered under the same name,
        /// any later registrations will override the previous.
        /// </remarks>
        /// <param name="format">The type to add.</param>
        /// <seealso cref="MetadataFormatValidatorAttribute"/>
        /// <seealso cref="FormatType(string)"/>
        /// <seealso cref="Validate(string, Stream)"/>
        public static void Register(Type format) {
            var attr = format.GetTypeInfo().GetCustomAttribute<MetadataFormatAttribute>(false);
            if (attr == null)
                throw new TypeLoadException("No explicit format name was passed, and the type has no attribute to infer it from");
            else
                Register(attr.Name, format);
        }
        /// <summary>
        /// Add the given type to the lookup tables under the specified custom
        /// descriptor, even if it does not have any associated
        /// <see cref="MetadataFormatAttribute"/>.
        /// </summary>
        /// <remarks>
        /// The validation function must still be identified with a
        /// <see cref="MetadataFormatValidatorAttribute"/>.
        /// <para/>
        /// Note that if multiple types are registered under the same name,
        /// any later registrations will override the previous.
        /// </remarks>
        /// <param name="name">
        /// A short name for the format to be used as an access key for later
        /// lookups.
        /// <para/>
        /// It is recommended that this also be exposed as a constant.
        /// </param>
        /// <param name="format">The type to add.</param>
        /// <seealso cref="FormatType(string)"/>
        /// <seealso cref="Validate(string, Stream)"/>
        public static void Register(string name, Type format) {
            if (typeof(ITagFormat).IsAssignableFrom(format) == false)
                throw new NotSupportedException("Metadata format types must implement ITagFormat");

            if (format.GetConstructor(new Type[1] { typeof(Stream) }) == null)
                throw new NotImplementedException("Metadata format types need a constructor taking only a Stream object");
            tagFormats[name] = format;

            foreach (var method in format.GetRuntimeMethods()
                    .Where((m) => m.IsDefined(typeof(MetadataFormatValidatorAttribute))))
                Register(name, method);
        }
        /// <summary>
        /// Add the given method to the lookup tables under the specified
        /// custom descriptor.
        /// <para/>
        /// This should almost purely be called via
        /// <see cref="Register(string, Type)"/>, and has been separated
        /// primarily for code clarity.
        /// </summary>
        /// <remarks>
        /// Note that if multiple methods are registered under the same name,
        /// any later registrations will override the previous.
        /// </remarks>
        /// <param name="name">
        /// A short name for the format to be used as an access key for later
        /// lookups.
        /// </param>
        /// <param name="method">The method to add.</param>
        private static void Register(string name, MethodInfo method) {
            if (method.IsPrivate)
                throw new NotSupportedException("Metadata format validation functions must not be private");
            if (method.IsAbstract)
                throw new NotSupportedException("Metadata format validation functions must not be abstract");
            if (method.IsStatic == false)
                throw new NotSupportedException("Metadata format validation functions must be static");

            var parameters = method.GetParameters();
            if ((parameters.Length == 0)
                || (parameters[0].ParameterType.IsAssignableFrom(typeof(Stream)) == false)
                || ((parameters.Length > 1) && (parameters[1].IsOptional == false))
                || (method.ReturnType != typeof(bool)))
                throw new NotSupportedException("Metadata format validation functions must be able to take only a Stream and return a bool");

            tagValidationFunctions[name] = (Func<Stream, bool>)method.CreateDelegate(typeof(Func<Stream, bool>));
        }

        /// <summary>
        /// Check the stream against all registered tag formats, and return
        /// those that match the header.
        /// </summary>
        /// <remarks>
        /// While, in theory, only a single header should match, the class
        /// structure is such that this is not a restriction; supporting this
        /// feature allows for nonstandard usages without exclusive headers.
        /// 
        /// The callee is left to determine the best means of handling the
        /// case of Detect(...).Count > 1.
        /// </remarks>
        /// <param name="stream">The bytestream to test.</param>
        /// <returns>The keys of all matching formats.</returns>
        /// <seealso cref="Validate(string, Stream)"/>
        public static List<ITagFormat> Parse(Stream stream) {
            var ret = new List<ITagFormat>(tagValidationFunctions.Count);

            //TODO: Wrap in `while` to handle multiple tags in the same file.
            // At that point, it may be best to return an object combining all
            // recognized tags (along with unknown data).

            //TODO: Operate on `byte[]` rather than `Stream` to avoid repeated
            // readings of the same segment and allow non-rewindable streams.
            foreach (var header in tagValidationFunctions)
                if (header.Value(stream))
                    ret.Add((ITagFormat)Activator.CreateInstance(tagFormats[header.Key], stream));

            return ret;
        }

        /// <summary>
        /// Parse metadata in the desired format from the current position in
        /// a stream.
        /// </summary>
        /// <param name="format">
        /// The short name of the metadata format.
        /// </param>
        /// <param name="stream">The bytestream to parse.</param>
        /// <returns>The parsed metadata.</returns>
        /// <seealso cref="FormatType(string)"/>
        public static ITagFormat Construct(string format, Stream stream) {
            return (ITagFormat)Activator.CreateInstance(tagFormats[format], stream);
        }
    }
}
