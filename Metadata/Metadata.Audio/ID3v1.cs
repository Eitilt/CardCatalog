using System;
using System.Linq;
using System.Reflection;
using System.Resources;

namespace Metadata.Audio {
	/// <summary>
	/// An implementation of the ID3v2.4 standard as described at
	/// <see href="http://id3.org/ID3v1"/>.
	/// </summary>
	public class ID3v1 {
		/// <summary>
		/// The list of genres defined by the standard.
		/// </summary>
		public enum Genre : byte {
			Blues = 0,
			ClassicRock = 1,
			Country = 2,
			Dance = 3,
			Disco = 4,
			Funk = 5,
			Grunge = 6,
			HipHop = 7,
			Jazz = 8,
			Metal = 9,
			NewAge = 10,
			Oldies = 11,
			Other = 12,
			Pop = 13,
			RB = 14,
			Rap = 15,
			Reggae = 16,
			Rock = 17,
			Techno = 18,
			Industrial = 19,
			Alternative = 20,
			Ska = 21,
			DeathMetal = 22,
			Pranks = 23,
			Soundtrack = 24,
			EuroTechno = 25,
			Ambient = 26,
			TripHop = 27,
			Vocal = 28,
			JazzFunk = 29,
			Fusion = 30,
			Trance = 31,
			Classical = 32,
			Instrumental = 33,
			Acid = 34,
			House = 35,
			Game = 36,
			SoundClip = 37,
			Gospel = 38,
			Noise = 39,
			AlternRock = 40,
			Bass = 41,
			Soul = 42,
			Punk = 43,
			Space = 44,
			Meditative = 45,
			InstrPop = 46,
			InstrRock = 47,
			Ethnic = 48,
			Gothic = 49,
			Darkwave = 50,
			TechnoIndustr = 51,
			Electronic = 52,
			PopFolk = 53,
			Eurodance = 54,
			Dream = 55,
			SouthernRock = 56,
			Comedy = 57,
			Cult = 58,
			Gangsta = 59,
			Top40 = 60,
			ChristianRap = 61,
			PopFunk = 62,
			Jungle = 63,
			NativeAm = 64,
			Cabaret = 65,
			NewWave = 66,
			Psychedelic = 67,
			Rave = 68,
			Showtunes = 69,
			Trailer = 70,
			LoFi = 71,
			Tribal = 72,
			AcidPunk = 73,
			AcidJazz = 74,
			Polka = 75,
			Retro = 76,
			Musical = 77,
			RockRoll = 78,
			HardRock = 79
		}
	}
	
	/// <summary>
	/// Extension methods for the <see cref="ID3v1"/> class.
	/// </summary>
	public static class ID3v1Extension {
		/// <summary>
		/// Cached reference to the resource dictionary to reduce reflection.
		/// </summary>
		private static ResourceManager resources = new ResourceManager("Metadata.Audio.Strings.ID3v1", typeof(ID3v1).GetTypeInfo().Assembly);

		/// <summary>
		/// Convert a <see cref="ID3v1.Genre"/> value to a human-readable
		/// string for the current locale.
		/// </summary>
		/// 
		/// <param name="value">The <see cref="ID3v1.Genre"/> to format.</param>
		/// 
		/// <returns>The formatted name.</returns>
		public static string PrintableName(this ID3v1.Genre value) {
			var str = value.ToString();

			if (str.All(char.IsDigit))
				return String.Format(resources.GetString("Genre.Unknown"), str);
			else
				return resources.GetString("Genre_" + str) ?? str;
		}
	}
}
