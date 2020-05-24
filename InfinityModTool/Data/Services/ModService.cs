using InfinityModTool.Data;
using InfinityModTool.Data.Utilities;
using InfinityModTool.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Services
{
	public class ModService
	{
		const string USER_SETTINGS = "UserSettings";

		//readonly ListOption[] ID_NAMES = new[]
		//{
		//	new ListOption("TRN_Quorra", "TRON - Quorra"),
		//	new ListOption("TRN_Sam", "TRON - Sam"),
		//	new ListOption("REB_Sabine", "Star Wars Rebels - Sabine"),
		//};

		readonly ListOption[] ID_NAMES = new[]
		{
			new ListOption("AL_Aladdin", "Disney - Aladdin"),
			new ListOption("AL_Jasmine", "Disney - Jasmine"),
			//new ListOption("IGP_AVG_Antman", "Marvel - Ant-Man"),
			new ListOption("AVG_BlackWidow", "Marvel - Black Widow"),
			new ListOption("AVG_CaptainAmerica", "Marvel - Captain America"),
			//new ListOption("IGP_AVG_CaptainMarvel", "Marvel - Captain Marvel"),
			new ListOption("AVG_Falcon", "Marvel - Falcon"),
			new ListOption("AVG_Hawkeye", "Marvel - Hawkeye"),
			new ListOption("AVG_Hulk", "Marvel - Hulk"),
			new ListOption("AVG_HulkBuster", "Marvel - Ironman (Hulkbuster)"),
			new ListOption("AVG_IronMan", "Marvel - Ironman"),
			//new ListOption("IGP_AVG_IronmanHulkBuster", "Marvel - Ironman (Hulkbuster)"),
			new ListOption("AVG_Loki", "Marvel - Loki"),
			new ListOption("AVG_Thor", "Marvel - Thor"),
			new ListOption("AVG_Ultron", "Marvel - Ultron"),
			//new ListOption("IGP_AVG_Vision", "Marvel - Vision"),
			new ListOption("AV_Buzz", "Disney - Buzz Lightyear"),
			new ListOption("AV_BuzzInfinite", "Disney - Buzz Lightyear (Special)"),
			new ListOption("AV_Buzz_Glow", "Disney - Buzz Lightyear (Glow in the Dark)"),
			//new ListOption("IGP_AV_Cars_Francesco", "Marvel - Hulk"),
			new ListOption("AV_Dash", "Disney - Dash"),
			new ListOption("AV_ElastiGirl", "Disney - Elastigirl"),
			//new ListOption("IGP_AV_Holly", "Marvel - Hulk"),
			new ListOption("AV_Jessie", "Disney - Jessie"),
			new ListOption("AV_Mater", "Disney - Mater"),
			new ListOption("AV_McQueen", "Disney - Lightning McQueen"),
			new ListOption("AV_McQueenInfinite", "Disney - Lightning McQueen (Special)"),
			new ListOption("AV_MrIncredible", "Disney - Mr Incredible"),
			new ListOption("AV_MrIncredibleInfinite", "Disney - Mr Incredible (Special)"),
			new ListOption("AV_Syndrome", "Disney - Syndrome"),
			new ListOption("AV_Violet", "Disney - Violet"),
			new ListOption("AV_Woody", "Disney - Woody"),
			new ListOption("BHS_Baymax", "Disney - Baymax"),
			new ListOption("BHS_Hiro", "Disney - Hiro"),
			new ListOption("BRV_Merida", "Disney - Merida"),
			new ListOption("DNO_Spot", "Disney - Spot"),
			new ListOption("EMP_BobaFett", "Star Wars - Boba Fett"),
			new ListOption("EMP_Chewbacca", "Star Wars - Chewbacca"),
			new ListOption("EMP_DarthVader", "Star Wars - Darth Vader"),
			new ListOption("EMP_HanSolo", "Star Wars - Han Solo"),
			new ListOption("EMP_Leia", "Star Wars- Princess Leia"),
			new ListOption("EMP_Luke", "Star Wars - Luke Skywalker"),
			new ListOption("FRO_Anna", "Disney - Anna"),
			new ListOption("FRO_Elsa", "Disney - Elsa"),
			new ListOption("FRO_Olaf", "Disney - Olaf"),
			new ListOption("GOG_Drax", "Marvel - Drax"),
			new ListOption("GOG_Gamora", "Marvel - Gamora"),
			new ListOption("GOG_Groot", "Marvel - Groot"),
			new ListOption("GOG_RocketRaccoon", "Marvel - Rocket Racoon"),
			new ListOption("GOG_Ronan", "Marvel - Ronan"),
			new ListOption("GOG_StarLord", "Marvel - Star Lord"),
			new ListOption("GOG_Yondu", "Marvel - Yondu"),
			new ListOption("LAS_Stitch", "Disney - Stitch"),
			new ListOption("LR_LoneRanger", "Lone Ranger - Lone Ranger"),
			new ListOption("LR_LoneRangerInfinite", "Lone Ranger - Lone Ranger (Special)"),
			new ListOption("LR_Tonto", "Lone Ranger - Tonto"),
			new ListOption("MAL_Maleficent", "Disney - Maleficent"),
			new ListOption("MU_Mike", "Monsters Inc - Mike Wazowski"),
			new ListOption("MU_Randall", "Monsters Inc - Randall"),
			new ListOption("MU_Sully", "Monsters Inc - Sully"),
			new ListOption("MU_SullyInfinite", "Monsters Inc - Sully (Special)"),
			new ListOption("NBC_JackSkellington", "Nightmare Before Christmas - Jack Skellington"),
			new ListOption("OUT_Anger", "Inside Out - Anger"),
			new ListOption("OUT_Disgust", "Inside Out - Disgust"),
			new ListOption("OUT_Fear", "Inside Out - Fear"),
			new ListOption("OUT_Joy", "Inside Out - Joy"),
			new ListOption("OUT_Sadness", "Inside Out - Sadness"),
			new ListOption("PIR_Barbossa", "Pirate of the Caribbean - Barbossa"),
			new ListOption("PIR_DavyJones", "Pirate of the Caribbean - Davy Jones"),
			new ListOption("PIR_JackSparrow", "Pirate of the Caribbean - Jack Sparrow"),
			new ListOption("PIR_JackSparrowInfinite", "Pirate of the Caribbean - Jack Sparrow (Special)"),
			new ListOption("PNF_Perry", "Disney - Perry"),
			new ListOption("PNF_PerryInfinite", "Disney - Perry (Special)"),
			new ListOption("PNF_Phineas", "Disney - Phineas"),
			new ListOption("REB_Ezra", "Star Wars - Ezra"),
			new ListOption("REB_Kanan", "Star Wars - Kanan"),
			new ListOption("REB_Sabine", "Star Wars - Sabine"),
			new ListOption("REB_Zeb", "Star Wars - Zeb"),
			new ListOption("SPD_GreenGoblin", "Marvel - Green Goblin"),
			new ListOption("SPD_IronFist", "Marvel - Iron Fist"),
			new ListOption("SPD_NickFury", "Marvel - Nick Fury"),
			//new ListOption("IGP_SPD_Nova", "Marvel - Nova"),
			new ListOption("SPD_Spiderman", "Marvel - Spiderman"),
			new ListOption("SPD_Spiderman_Black", "Marvel - Spiderman (Black Suit)"),
			new ListOption("SPD_Venom", "Marvel - Venom"),
			new ListOption("TAN_Rapunzel", "Disney - Rapunzel"),
			new ListOption("TBX_ClassicMickey", "Disney - Classic Mickey"),
			//new ListOption("IGP_TBX_JudyHopps", "Marvel - Hulk"),
			new ListOption("TBX_Minnie", "Disney - Minnie"),
			new ListOption("TBX_Mulan", "Disney - Mulan"),
			//new ListOption("IGP_TBX_NickWilde", "Marvel - Hulk"),
			new ListOption("TCW_Ahsoka", "Star Wars - Ahsoka"),
			new ListOption("TCW_Anakin", "Star Wars - Anakin"),
			new ListOption("TCW_DarthMaul", "Star Wars - Darth Maul"),
			new ListOption("TCW_ObiWan", "Star Wars - Obi-Wan"),
			new ListOption("TCW_Yoda", "Star Wars - Yoda"),
			//new ListOption("IGP_TRN_Clu", "TRON - CLU"),
			new ListOption("TRN_Quorra", "TRON - Quorra"),
			new ListOption("TRN_Sam", "TRON - Sam"),
			new ListOption("WR_Ralph", "Wreck-it-Ralph - Ralph"),
			new ListOption("WR_Vanellope", "Wreck-it-Ralph - Venellope"),
			new ListOption("ZOT_JudyHopps", "Zootopia - Judy Hopps"),
			new ListOption("ZOT_NickWilde", "Zootopia - Nick Wilde")
		};

		public UserModData Settings = new UserModData();
		public CharacterData[] AvailableCharacterMods = new CharacterData[0];

		public ModService()
		{
			this.Settings = SettingsUtility.LoadSettings<UserModData>(USER_SETTINGS);

			if (Settings.InstalledCharacterMods == null)
				Settings.InstalledCharacterMods = new List<CharacterModLink>();

			this.AvailableCharacterMods = ModLoaderService.GetAvailableCharacterMods();
		}

		public bool IsModInstalled(string idName)
		{
			return Settings.InstalledCharacterMods.Any(m => m.CharacterID == idName);
		}

		public ListOption[] GetAvailableReplacementCharacters(string idName)
		{
			var takenNames = Settings.InstalledCharacterMods.Select(i => i.ReplacementCharacterID);
			var potentialNamePool = ID_NAMES.Where(n => n.Value.Length == idName.Length && !takenNames.Contains(n.Value));
			
			return potentialNamePool.ToArray();
		}

		public CharacterData GetCharacterData(string idName)
		{
			return AvailableCharacterMods.FirstOrDefault(m => m.Name == idName);
		}

		public async Task InstallCharacterMod(string idName, string replacementIDName)
		{
			var modToAdd = new CharacterModLink() { CharacterID = idName, ReplacementCharacterID = replacementIDName };
			Settings.InstalledCharacterMods.Add(modToAdd);

			await UpdateModConfiguration();
			SaveSettings();
		}

		public async Task UninstallCharacterMod(string idName)
		{
			var modToRemove = Settings.InstalledCharacterMods.FirstOrDefault(m => m.CharacterID == idName);
			Settings.InstalledCharacterMods.Remove(modToRemove);

			await UpdateModConfiguration();
			SaveSettings();
		}

		public void SaveSettings()
		{
			SettingsUtility.SaveSettings(Settings, USER_SETTINGS);
		}

		private async Task UpdateModConfiguration()
		{
			if (!CheckSteamPathSettings())
				throw new System.Exception("Cannot apply mods if the steam installation path has not been set");

			var configuration = new Configuration() { SteamInstallationPath = Settings.SteamInstallationPath };
			var characterModifications = Settings.InstalledCharacterMods.Select(i => new CharacterModification() { 
				Data = AvailableCharacterMods.First(c => c.Name == i.CharacterID),
				ReplacementCharacter = i.ReplacementCharacterID
			}).ToArray();

			await ModUtility.ApplyChanges(configuration, characterModifications);
		}

		private bool CheckSteamPathSettings()
		{
			return !string.IsNullOrEmpty(Settings.SteamInstallationPath);
		}
	}
}
