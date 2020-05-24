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

		public ListOption[] IDNames { get; private set; }

		public UserModData Settings = new UserModData();
		public CharacterData[] AvailableCharacterMods = new CharacterData[0];

		public ModService()
		{
			this.Settings = SettingsUtility.LoadSettings<UserModData>(USER_SETTINGS);

			if (Settings.InstalledCharacterMods == null)
				Settings.InstalledCharacterMods = new List<CharacterModLink>();

			this.IDNames = ModLoaderService.GetIDNameListOptions();
			this.AvailableCharacterMods = ModLoaderService.GetAvailableCharacterMods();
		}

		public bool IsModInstalled(string idName)
		{
			return Settings.InstalledCharacterMods.Any(m => m.CharacterID == idName);
		}

		public ListOption[] GetAvailableReplacementCharacters(string idName)
		{
			var takenNames = Settings.InstalledCharacterMods.Select(i => i.ReplacementCharacterID);
			var potentialNamePool = IDNames.Where(n => n.Value.Length == idName.Length && !takenNames.Contains(n.Value));
			
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
