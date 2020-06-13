using InfinityModTool.Data;
using InfinityModTool.Data.Modifications;
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

		public ModService()
		{
			this.Settings = SettingsUtility.LoadSettings<UserModData>(USER_SETTINGS);

			if (Settings.InstalledMods == null)
				Settings.InstalledMods = new List<ModInstallationData>();

			this.IDNames = ModLoaderService.GetIDNameListOptions();
			this.AvailableMods = ModLoaderService.LoadMods(CurrentVersion);
		}

		public IEnumerable<T> GetInstalledMods<T>()
			where T : ModInstallationData
		{
			return Settings.InstalledMods.Where(i => i is T).Cast<T>();
		}

		public bool IsModInstalled(string modID)
		{
			return Settings.InstalledMods.Any(m => m.ModID == modID);
		}

		public string GetModIcon(string modID)
		{
			var modData = GetMod(modID);

			switch (modData.ModCategory)
			{
				case "Character":
					return (modData as CharacterModConfiguration).ReplaceCharacter ? "mod-replace.svg" : "mod-noreplace.svg";
				default:
					return null;
			}
		}

		public ListOption[] GetAvailableReplacementCharacters(string idName)
		{
			var takenNames = Settings.InstalledMods.Where(i => i.ModCategory == "Character").Select(i => i.Parameters["ReplacementCharacter"]);
			var potentialNamePool = IDNames.Where(n => n.Value.Length == idName.Length && !takenNames.Contains(n.Value));
			
			return potentialNamePool.ToArray();
		}

		public IEnumerable<BaseModConfiguration> GetModsForCategory(string category)
		{
			if (category == null)
				return AvailableMods;
			else
				return AvailableMods.Where(m => (m.ModCategory ?? string.Empty).Equals(category, System.StringComparison.InvariantCultureIgnoreCase));
		}

		public BaseModConfiguration GetMod(string modID)
		{
			return AvailableMods.FirstOrDefault(m => m.ModID == modID);
		}

		public T GetMod<T>(string modID)
			where T : BaseModConfiguration
		{
			return AvailableMods.FirstOrDefault(m => m.ModID == modID && m is T) as T;
		}

		public async Task InstallCharacterMod(ModInstallationData modData)
		{
			Settings.InstalledMods.Add(modData);

			await UpdateModConfiguration();
			SaveSettings();
		}

		public async Task UninstallMod(string idName)
		{
			var modToRemove = Settings.InstalledMods.FirstOrDefault(m => m.ModID == idName);
			Settings.InstalledMods.Remove(modToRemove);

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

			var mods = Settings.InstalledMods.Select(i => new GameModification() { 
				Config = GetMod(i.ModID),
				Parameters = i.Parameters
			}).ToArray();

			await ModUtility.ApplyChanges(configuration, mods);
		}

		private bool CheckSteamPathSettings()
		{
			return !string.IsNullOrEmpty(Settings.SteamInstallationPath);
		}
	}
}
