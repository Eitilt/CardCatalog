using System;

namespace TmpTest {
	class Program {
		static void Main(string[] args) {
			AgEitilt.CardCatalog.FormatRegistry.RegisterAll<AgEitilt.CardCatalog.Audio.AudioTagFormat>();
			var _ = AgEitilt.CardCatalog.FormatRegistry.ParseAsync(System.IO.File.OpenRead(@"..\..\..\..\..\..\Shared\Music\Malukah\Misty Mountains.mp3")).Result;
		}
	}
}