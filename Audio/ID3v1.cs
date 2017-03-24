/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Linq;

namespace AgEitilt.CardCatalog.Audio {
	/// <summary>
	/// An implementation of the ID3v2.4 standard as described at
	/// <see href="http://id3.org/ID3v1"/>.
	/// </summary>
	public class ID3v1 {
		/// <summary>
		/// The list of genres defined by the standard.
		/// </summary>
		public enum Genre : byte {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
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
			HardRock = 79,
			// Additional genres as defined by Winamp
			Folk = 80,
			FolkRock = 81,
			NationalFolk = 82,
			Swing = 83,
			FastFusion = 84,
			Bebop = 85,
			Latin = 86,
			Revival = 87,
			Celtic = 88,
			Bluegrass = 89,
			Avantgarde = 90,
			GothicRock = 91,
			ProgRock = 92,
			PsychRock = 93,
			SymphRock = 94,
			SlowRock = 95,
			BigBand = 96,
			Chorus = 97,
			EasyListening = 98,
			Acoustic = 99,
			Humor = 100,
			Speech = 101,
			Chanson = 102,
			Opera = 103,
			ChamberMusic = 104,
			Sonata = 105,
			Symphony = 106,
			BootyBass = 107,
			Primus = 108,
			PornGroove = 109,
			Satire = 110,
			SlowJam = 111,
			Club = 112,
			Tango = 113,
			Samba = 114,
			Folklore = 115,
			Ballad = 116,
			PowerBallad = 117,
			RhythmicSoul = 118,
			Freestyle = 119,
			Duet = 120,
			PunkRock = 121,
			DrumSolo = 122,
			ACapella = 123,
			EuroHouse = 124,
			DanceHall = 125,
			// Further supplemental genres of unknown provenance
			Goa = 126,
			DrumBass = 127,
			ClubHouse = 128,
			Hardcore = 129,
			Terror = 130,
			Indie = 131,
			BritPop = 132,
			AfroPunk = 133,
			PolskPunk = 134,
			Beat = 135,
			ChristianGangstaRap = 136,
			HeavyMetal = 137,
			BlackMetal = 138,
			Crossover = 139,
			ContempChristian = 140,
			ChristianRock = 141,
			Merengue = 142,
			Salsa = 143,
			ThrashMetal = 144,
			Anime = 145,
			JPop = 146,
			Synthpop = 147,
			// Even-less-standardized genres, attributed only at
			// <http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/ID3.html>
			Abstract = 148,
			ArtRock = 149,
			Baroque = 150,
			Bhangra = 151,
			BigBeat = 152,
			Breakbeat = 153,
			Chillout = 154,
			Downtempo = 155,
			Dub = 156,
			EBM = 157,
			Eclectic = 158,
			Electro = 159,
			Electroclash = 160,
			Emo = 161,
			Experimental = 162,
			Garage = 163,
			Global = 164,
			IDM = 165,
			Illbient = 166,
			IndustroGoth = 167,
			JamBand = 168,
			Krautrock = 169,
			Leftfield = 170,
			Lounge = 171,
			MathRock = 172,
			NewRomantic = 173,
			NuBreakz = 174,
			PostPunk = 175,
			PostRock = 176,
			Psytrance = 177,
			Shoegaze = 178,
			SpaceRock = 179,
			TropRock = 180,
			WorldMusic = 181,
			Neoclassical = 182,
			Audiobook = 183,
			AudioTheatre = 184,
			NeueDeutscheWelle = 185,
			Podcast = 186,
			IndieRock = 187,
			GFunk = 188,
			Dubstep = 189,
			GarageRock = 190,
			Psybient = 191,
			None = 255
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
		}
	}
	
	/// <summary>
	/// Extension methods for the <see cref="ID3v1"/> class.
	/// </summary>
	public static class ID3v1Extension {
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

			// If `str` is purely digits, the lookup has failed
			if (str.All(char.IsDigit) == false) {
				var genre = Strings.ID3v1.ResourceManager.GetString("Genre_" + str);
				if (genre != null)
					return genre;
			}
			return String.Format(Strings.ID3v1.Genre_Unknown, str);
		}
	}
}
