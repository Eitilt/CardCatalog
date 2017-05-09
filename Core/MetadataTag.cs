/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace AgEitilt.CardCatalog {
	/// <summary>
	/// Common properties to retrieve info from multiple tag formats.
	/// </summary>
	public abstract class MetadataTag : IParsable {
		/// <summary>
		/// The specific logger instance used for methods within this or
		/// derived types.
		/// </summary>
		protected readonly ILogger logger;

		/// <summary>
		/// Initialize the fields common to all derived instances.
		/// </summary>
		public MetadataTag() {
			logger = FormatRegistry.LoggerFactory?.CreateLogger(GetType().FullName);
		}

		/// <summary>
		/// The display name of the tag format.
		/// </summary>
		public abstract string Format { get; }

		/// <summary>
		/// The length in bytes of the tag, not including the header.
		/// </summary>
		/// 
		/// <remarks>
		/// The underlying value should be set in any function marked with
		/// <see cref="HeaderParserAttribute"/>; if that function
		/// sets it to 0, the incoming stream will be read according to
		/// </remarks>
		public int Length { get; protected set; }

		/// <summary>
		/// The low-level representations of the tag data.
		/// </summary>
		public IEnumerable<TagField> Fields { get; private set; }

		/// <summary>
		/// The proper standardized field redirects for the enclosing
		/// metadata format.
		/// </summary>
		/// 
		/// <seealso cref="Fields"/>
		public abstract ITagAttributes Attributes { get; }

		/// <summary>
		/// Parse the fields contained within a tag.
		/// </summary>
		/// 
		/// <remarks>
		/// TODO: Handle tags with unknown length.
		/// </remarks>
		/// 
		/// <param name="stream">The stream to read.</param>
		public void Parse(Stream stream) {
			Fields = ReflectionData<TagField>.ParseAsync(stream, FormatRegistry.tagFormats[Format].fields.Values).Result;
		}
	}
}
