using System;
using System.Collections.Generic;
using System.IO;

namespace Metadata {
    public static class Metadata {
        /// <summary>
        /// Validation functions for each registered metadata format.
        /// </summary>
        /// <seealso cref="Validate(string, Stream)"/>
        private static Dictionary<string, Func<Stream, bool>> tagHeaders;
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
        /// <seealso cref="Detect(Stream)"/>
        public static bool Validate(string format, Stream stream) {
            return tagHeaders[format](stream);
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
        /// Add an implementation of a metadata tag reader to the list of
        /// automatically-handlable types.
        /// </summary>
        /// <typeparam name="TFormat">
        /// The type encapsulating the metadata format.
        /// <para/>
        /// Note that this must include a constructor taking only a Stream.
        /// TODO: Validate that the type actually has such a constructor
        /// </typeparam>
        /// <param name="format">
        /// A short name for the format to be used as an access key for later
        /// lookups.
        /// <para/>
        /// It is recommended that this be exposed as a constant.
        /// </param>
        /// <param name="header">
        /// A validation function returning `true` if the stream begins with
        /// metadata in the proper format.
        /// <para/>
        /// Note that this function should leave Stream.Position with the same
        /// value as it was before being called, but should not assume that
        /// (Stream.Position == 0).
        /// </param>
        /// <seealso cref="FormatType(string)"/>
        /// <seealso cref="Validate(string, Stream)"/>
        public static void Register<TFormat>(string format, Func<Stream, bool> header) where TFormat : Metadata {
            tagFormats[format] = typeof(TFormat);
            tagHeaders[format] = header;
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
        public static List<string> Detect(Stream stream) {
            var ret = new List<string>(tagHeaders.Count);

            foreach (var header in tagHeaders)
                if (header.Value(stream))
                    ret.Add(header.Key);

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
        public static ITag Construct(string format, Stream stream) {
            return (ITag)Activator.CreateInstance(tagFormats[format], stream);
        }

        /// <summary>
        /// Common methods to operate on metadata tags.
        /// </summary>
        public interface ITag { }
    }
}
