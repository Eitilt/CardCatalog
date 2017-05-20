namespace AgEitilt.CardCatalog {
	/// <summary>
	/// A wrapper around raw image data to distinguish it as such.
	/// </summary>
	public struct ImageData {
		/// <summary>
		/// The encoding of this image, as a standard MIME type string.
		/// </summary>
		public string Type { get; set; }

		/// <summary>
		/// The raw binary data describing the image.
		/// </summary>
		public byte[] Data { get; set; }
	}
}
