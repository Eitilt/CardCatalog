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
		public ImageData(byte[] data) : this() {
			Data = data;
		}

		/// <summary>
		/// The raw binary data describing the image.
		/// </summary>
		public byte[] Data { get; private set; }
	}
}
