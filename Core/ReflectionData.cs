/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using AgEitilt.Common.Stream.Extensions;

using Microsoft.Extensions.Logging;

namespace AgEitilt.CardCatalog {
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
		/// <summary>
		/// The specific logger instance used for methods within
		/// <see cref="ReflectionData{T}"/>.
		/// </summary>
		static readonly ILogger<ReflectionData<T>> logger = FormatRegistry.LoggerFactory?.CreateLogger<ReflectionData<T>>();

		public Type Type { get; set; }
		/// <summary>
		/// All functions that might return an instance of <c>T</c> when given
		/// a byte header.
		/// </summary>
		public List<HeaderValidation<T>> ValidationFunctions { get; } = new List<HeaderValidation<T>>(1);

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
		internal static async Task<IEnumerable<T>> ParseAsync(Stream stream, IEnumerable<ReflectionData<T>> types) {
			logger?.LogDebug(Strings.Logger.GenericParse, typeof(T).FullName);

			var tasks = new List<Task>();
			var ret = new List<T>();

			//TODO: It may be best to return an object explicitly combining
			// all recognized tags along with unknown data.

			while (true) {
				// Keep track of all bytes read for headers, to avoid
				// unnecessarily repeating stream accesses
				var readBytes = new List<byte>();

				bool found = false;
				foreach (var v in types.SelectMany(t => t.ValidationFunctions)) {
					// Make sure we have enough bytes to check the header
					if (readBytes.Count < v.length) {
						logger?.LogDebug(Strings.Logger.GenericParse_Header, (v.length - readBytes.Count), v.length);

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
					if (tag == null) {
						// The header was invalid, and so this function isn't
						// the correct type to parse next
						logger?.LogTrace(Strings.Logger.GenericParse_Skip);
						continue;
					} else {
						logger?.LogDebug(Strings.Logger.GenericParse_Found, tag.GetType().FullName);
					}

					ret.Add(tag);

					if (tag.Length == 0) {
						logger?.LogDebug(Strings.Logger.GenericParse_Bound_Unknown);

						/* The header doesn't contain length data, so we need
						 * to read the stream directly until whatever end-of-
						 * data the format uses is reached
						 */
						//BUG: This won't include any bytes already read for
						// the header
						tag.Parse(stream);
					} else {
						logger?.LogDebug(Strings.Logger.GenericParse_Bound, tag.Length);

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
