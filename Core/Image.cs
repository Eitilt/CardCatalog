using System;
using System.Linq;

namespace AgEitilt.CardCatalog {
	/// <summary>
	/// A wrapper around raw image data to distinguish it as such.
	/// </summary>
	public struct ImageData {
		/// <summary>
		/// Wrap the given data as an image.
		/// </summary>
		/// 
		/// <param name="data">The raw, binary data.</param>
		/// <param name="mime">The MIME type of the image.</param>
		public ImageData(byte[] data, string mime) : this() {
			Data = data;
			Type = mime;
		}

		/// <summary>
		/// The encoding of this image, as a standard MIME type string.
		/// </summary>
		public string Type { get; }

		/// <summary>
		/// The raw binary data describing the image.
		/// </summary>
		public byte[] Data { get; }
	}
}
