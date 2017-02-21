using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Metadata.Audio {
    /// <summary>
    /// An implementation of the ID3v2.4 standard as described at
    /// <see cref="http://id3.org/id3v2.4.0-structure"/> and
    /// <see cref="http://id3.org/id3v2.4.0-frames"/>
    /// </summary>
    /// <remarks>
    /// TODO: Handle footer
    /// </remarks>
    internal class ID3v24 : ID3v23Plus {
        /// <summary>
        /// The short name used to represent ID3v2.4 metadata.
        /// </summary>
        /// <seealso cref="MetadataFormat.Register{TFormat}(string, Func{Stream, bool})"/>
        public const string format = "ID3v2.4";

        /// <summary>
        /// Describe the behaviour of the extended header.
        /// </summary>
        public override ExtendedHeaderProps ExtendedHeader => new ExtendedHeaderProps {
            sizeIncludesItself = true,
            bitsInSize = 7
        };

        /// <summary>
        /// Register the format with the superclass.
        /// </summary>
        /// <seealso cref="MetadataFormat.Register{TFormat}(string, Func{Stream, bool})"/>
        static ID3v24() {
            MetadataFormat.Register<ID3v24>(format, VerifyHeader);
        }

        /// <summary>
        /// Check whether the stream begins with a valid ID3v2.4 header.
        /// </summary>
        /// <param name="stream">The Stream to check.</param>
        /// <returns>
        /// Whether the stream begins with a valid ID3v2.4 header.
        /// </returns>
        /// <see cref="MetadataFormat.Validate(string, Stream)"/>
        public static bool VerifyHeader(Stream stream) {
            return (VerifyBaseHeader(stream)?.Equals(0x04) ?? false);
        }
        /// <summary>
        /// Check whether the byte array begins with a valid ID3v2.4 header.
        /// </summary>
        /// <param name="header">The byte array to check</param>
        /// <returns>
        /// `null` if the stream does not begin with a ID3v2.4 header, and the
        /// major version if it does.
        /// </returns>
        public static bool VerifyHeader(byte[] header) {
            return (VerifyBaseHeader(header)?.Equals(0x04) ?? false);
        }

        /// <summary>
        /// Implement the audio field attribute mappings for ID3v2.3 tags.
        /// </summary>
        class AttributeStruct : AudioTagAttributes { }

        /// <summary>
        /// Retrieve the audio field attribute mappings for ID3v2.3 tags.
        /// </summary>
        public override AudioTagAttributes Attributes => new AttributeStruct();

        /// <summary>
        /// Whether the tag is closed with a footer.
        /// </summary>
        bool HasFooter { get; set; }

        /// <summary>
        /// Whether this tag updates any previous tags
        /// </summary>
        bool TagIsUpdate { get; set; }

        /// <summary>
        /// Parse a stream according the proper version of the ID3v2
        /// specification, from the current location.
        /// </summary>
        /// <remarks>
        /// As according to the recommendation in the ID3v2.2 specification,
        /// if the tag is compressed, it is swallowed but largely ignored.
        /// </remarks>
        /// <param name="stream">The stream to parse.</param>
        /// <exception cref="FormatException">
        /// This class can only parse metadata in ID3v2.2 to ID3v2.4 formats,
        /// and fails if the stream position is not placed at the beginning of
        /// that tag.
        /// <param/>
        /// If this is thrown, the stream cursor is automatically returned to
        /// the position it was at before the constructor was called.
        /// </exception>
        /// <seealso cref="MetadataFormat.Construct(string, Stream)"/>
        public ID3v24(Stream stream) {
            HasFooter = false;
            TagIsUpdate = false;

            byte[] tag = ParseHeaderAsync(stream).Result;
            if (CheckCRCIfPresent(tag) == false)
                throw new InvalidDataException("ID3 tag does not match saved checksum");
        }

        /// <summary>
        /// Extract and encapsulate the code used to parse a ID3v2 header into
        /// usable variables, and use that to retrieve the rest of the tag.
        /// </summary>
        /// <param name="stream">The stream to parse.</param>
        /// <returns>
        /// The remainder of the ID3v2.4 tag, already processed to reverse any
        /// unsynchronization.
        /// </returns>
        async Task<byte[]> ParseHeaderAsync(Stream stream) {
            var header = ParseBaseHeader(stream, VerifyHeader);

            bool useUnsync = header.Item1[0];
            // flags[1] is handled below
            IsExperimental = header.Item1[2];
            HasFooter = header.Item1[3];
            /*TODO: May be better to skip reading the tag rather than setting
             * FlagUnknown, as these flags tend to be critical to the proper
             * parsing of the tag.
             */
            FlagUnknown = (header.Item1.Cast<bool>().Skip(4).Contains(true));

            return await ReadExtHeaderWithTagAsync(stream, header.Item2, useUnsync, header.Item1[1]).ConfigureAwait(false);
        }

        /// <summary>
        /// Extract and encapsulate the code used to parse a ID3v2 extended
        /// header into usable variables.
        /// <para/>
        /// Given that arrays have an inherent Length property, the first four
        /// bytes (storing the size) are ignored.
        /// </summary>
        /// <remarks>
        /// This takes a `byte[]` rather than a `Stream` like
        /// <see cref="ParseHeaderAsync(Stream)"/> because this is intended to
        /// be /// called on pre-processed data of the proper length, rather
        /// than the raw bytestream.
        /// </remarks>
        /// <param name="extHeader">
        /// The de-unsynchronized byte array to parse.
        /// </param>
        protected override void ParseExtendedHeader(byte[] extHeader) {
            //TODO: Doesn't ensure that the length is enough for the flag data
            // and so array index out-of-bounds exceptions may be thrown.
            if (extHeader.Length < 2)
                throw new InvalidDataException("Extended header too short to be valid for ID3v2.4");

            int flagBytes = (int)ParseInteger(new byte[1]{ extHeader[0] });
            var flags = new BitArray(extHeader.Skip(1).Take(flagBytes).ToArray());

            int pos = flagBytes + 1;

            // Shouldn't be set according to the standard, but non-traditional
            // encoders may have assigned some meaning to it
            if (flags[0]) {
                FlagUnknown = true;
                pos += extHeader[pos] + 1;
            }
            if (flags[1]) {
                if (extHeader[pos] != 0x00)
                    throw new InvalidDataException("Invalid length (" + extHeader[pos] + ") given for ID3v2.4 'Tag is an update' data");
                TagIsUpdate = true;
                ++pos;
            }
            if (flags[2]) {
                if (extHeader[pos] != 0x05)
                    throw new InvalidDataException("Invalid length (" + extHeader[pos] + ") given for ID3v2.4 'CRC data present' data");
                TagCRC = ParseInteger(extHeader.Skip(pos + 1).Take(5), 7);
                pos += 6;
            }
            if (flags[3]) {
                if (extHeader[pos] != 0x01)
                    throw new InvalidDataException("Invalid length (" + extHeader[pos] + ") given for ID3v2.4 'Tag restrictions' data");
                //TODO: This only affects behaviour before encoding, but
                // should still be handled
                ++pos;
            }
            FlagUnknown = (flags.Cast<bool>().Skip(4).Contains(true));
        }
    }
}