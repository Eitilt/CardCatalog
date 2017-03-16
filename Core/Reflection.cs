using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CardCatalog {
	internal interface IParsable {
		int Length { get; }
		void Parse(Stream stream);
	}

	internal struct HeaderValidation<H> {
		public int length;
		public delegate H Validator(IEnumerable<byte> header);
		public Validator function;
	}

	internal class ReflectionData<T> where T : IParsable {
		public Type type;
		public List<HeaderValidation<T>> validationFunctions = new List<HeaderValidation<T>>(1);

		/// <summary>
		/// Check the stream against all registered <see cref="IParsable"/>
		/// formats, and return the sequence the occur in within the file.
		/// </summary>
		/// 
		/// <remarks>
		/// It is expected that no more than a single validation function will
		/// match any given sequence of bytes. If this is not the case, only
		/// the first-matched will be generated.
		/// <para/>
		/// TODO: It may be best to return an object explicitly combining
		/// all recognized tags <em>along with</em> unknown data.
		/// </remarks>
		/// 
		/// <param name="stream">The bytestream to parse.</param>
		/// <param name="types">
		/// The types that might be contained in the stream.
		/// </param>
		/// 
		/// <returns>The generated objects.</returns>
		public static async Task<IEnumerable<T>> ParseAsync(Stream stream, IEnumerable<ReflectionData<T>> types) {
			var tasks = new List<Task>();
			var ret = new List<T>();

			//TODO: It may be best to return an object explicitly combining
			// all recognized tags along with unknown data.

			while (true) {
				// Keep track of all bytes read for headers, to avoid
				// unnecessarily repeating stream accesses
				var readBytes = new List<byte>();

				bool found = false;
				foreach (var v in types.SelectMany(t => t.validationFunctions)) {
					// Make sure we have enough bytes to check the header
					/* Automatically leaves the stream untouched if we've
					 * already read enough of a header
					 */
					if (readBytes.Count < v.length) {
						var buffer = new byte[v.length - readBytes.Count];
						var readCount = stream.ReadAll(buffer, 0, buffer.Length);

						// If the stream has ended before the entire
						// header can fit, then the header's not present
						if (readCount < buffer.Length)
							continue;
						else
							readBytes.AddRange(buffer);
					}

					// Try to construct an empty object of the current format
					var tag = v.function(readBytes.Take(v.length));
					if (tag == null)
						// The header was invalid, and so this function isn't
						// the correct type to parse next
						continue;

					ret.Add(tag);

					if (tag.Length == 0) {
						/* The header doesn't contain length data, so we need
						 * to read the stream directly until whatever end-of-
						 * data the format uses is reached
						 */
						//BUG: This won't include any bytes already read for
						// the header
						tag.Parse(stream);
					} else {
						// Read all data from the stream before parsing the
						// next object along
						var bytes = new byte[tag.Length];
						// Restore any bytes previously read for the header
						readBytes.Skip(v.length).ToArray().CopyTo(bytes, 0);
						int offset = (readBytes.Count - v.length);

						/* In order to properly continue to the next tag in
						 * the file while the processing is performed as async
						 * we need to advance the stream beyond the current
						 * tag; given that Stream.Read*(...) isn't guaranteed
						 * to read the full count of bytes, we need to do this
						 * in a loop.
						 */
						while (offset < tag.Length) {
							var read = await stream.ReadAsync(bytes, offset, (tag.Length - offset));

							if (read == 0) {
								//throw new EndOfStreamException("End of the stream was reached before the expected length of the final tag");
								bytes = bytes.Take(offset).ToArray();
								break;
							} else {
								offset += read;
							}
						}

						// Split off the processing to return to IO-bound code
						tasks.Add(Task.Run(() => {
							using (var ms = new MemoryStream(bytes))
								tag.Parse(ms);
						}));
					}

					// All failure conditions were checked before this, so end
					// the "for the first matched header" loop
					found = true;
					break;
				}

				if (found == false)
					break;
			}

			await Task.WhenAll(tasks);

			return ret;
		}
	}
}
