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
		const double CurrentVersion = 1.0;

		public ListOption[] IDNames { get; private set; }

		public UserModData Settings = new UserModData();
		public BaseModConfiguration[] AvailableMods = new BaseModConfiguration[0];
		public IEnumerable<CharacterModConfiguration> AvailableCharacterMods => AvailableMods.Where(m => m is CharacterModConfiguration).Cast<CharacterModConfiguration>();

		public ModService()
		{
			this.Settings = SettingsUtility.LoadSettings<UserModData>(USER_SETTINGS);

			if (Settings.InstalledCharacterMods == null)
				Settings.InstalledCharacterMods = new List<CharacterModLink>();

			this.IDNames = ModLoaderService.GetIDNameListOptions();
			this.AvailableMods = ModLoaderService.LoadMods(CurrentVersion);
		}

		public bool IsModInstalled(string modID)
		{
			return Settings.InstalledCharacterMods.Any(m => m.ModID == modID);
		}

		public ListOption[] GetAvailableReplacementCharacters(string idName)
		{
			var takenNames = Settings.InstalledCharacterMods.Select(i => i.ReplacementCharacterID);
			var potentialNamePool = IDNames.Where(n => n.Value.Length == idName.Length && !takenNames.Contains(n.Value));
			
			return potentialNamePool.ToArray();
		}

		public CharacterModConfiguration GetCharacterMod(string modID)
		{
			return AvailableCharacterMods.FirstOrDefault(m => m.ModID == modID);
		}

		public async Task InstallCharacterMod(string modID, string replacementIDName)
		{
			var modToAdd = new CharacterModLink() { ModID = modID, ReplacementCharacterID = replacementIDName };
			Settings.InstalledCharacterMods.Add(modToAdd);

			await UpdateModConfiguration();
			SaveSettings();
		}

		public async Task UninstallCharacterMod(string idName)
		{
			var modToRemove = Settings.InstalledCharacterMods.FirstOrDefault(m => m.ModID == idName);
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
				Config = AvailableCharacterMods.First(c => c.ModID == i.ModID),
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
