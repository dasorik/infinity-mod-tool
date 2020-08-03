using InfinityModTool.Data;
using InfinityModTool.Data.Modifications;
using InfinityModTool.Data.Utilities;
using InfinityModTool.Enums;
using InfinityModTool.Models;
using InfinityModTool.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tewr.Blazor.FileReader;

namespace InfinityModTool.Services
{
	public class ModService
	{
		const string USER_SETTINGS = "UserSettings";
		const double CurrentVersion = 2.0;

		public ListOption[] IDNames { get; private set; }

		public UserModData Settings = new UserModData();
		public BaseModConfiguration[] AvailableMods = new BaseModConfiguration[0];

		public bool ModLoadWarningShown = false;
		public List<ModLoaderUtility.ModLoadResult> ModLoadResults = new List<ModLoaderUtility.ModLoadResult>();


		public ModService()
		{
			Logging.LogMessage("Loading user settings...", Logging.LogSeverity.Info);
			this.Settings = SettingsUtility.LoadSettings<UserModData>(USER_SETTINGS);

			if (Settings.InstalledMods == null)
				Settings.InstalledMods = new List<ModInstallationData>();

			this.IDNames = ModUtility.GetIDNameListOptions();
			ReloadMods();
		}

		public void ReloadMods()
		{
			Logging.LogMessage("Loading Mods...", Logging.LogSeverity.Info);

			this.ModLoadWarningShown = false;
			this.AvailableMods = ModLoaderUtility.LoadMods(CurrentVersion, ModLoadResults);

			foreach (var mod in this.ModLoadResults)
			{
				if (mod.loadErrors.Count() == 0)
					continue;

				foreach (var error in mod.loadErrors)
					Logging.LogMessage($"{mod.modFileName} - {error}", Logging.LogSeverity.Error);
			}

			if (ModLoadResults.All(m => m.status == ModLoadStatus.Success))
				Logging.LogMessage("Loaded all mods successfully!", Logging.LogSeverity.Info);
		}

		public ModLoadStatus TryAddMod(string fileName, byte[] fileBytes)
		{
			var modInstallPath = Path.Combine(ModLoaderUtility.GetModPath(), fileName);

			if (File.Exists(modInstallPath))
				modInstallPath = FileWriterUtility.GetUniqueFilePath(modInstallPath);

			var fileInfo = new FileInfo(modInstallPath);
			File.WriteAllBytes(modInstallPath, fileBytes);

			ReloadMods();

			var result = ModLoadResults.First(r => r.modFileName == fileInfo.Name).status;
			var success = result == ModLoadStatus.Success;

			if (!success)
			{
				// If this mod failed to load, 
				File.Delete(modInstallPath);
				ReloadMods();
			}

			return result;
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

			if (modData.ModCategory.Equals("Character", System.StringComparison.InvariantCultureIgnoreCase))
				return "mod-type-character.svg";

			if (modData.ModCategory.Equals("Playset", System.StringComparison.InvariantCultureIgnoreCase))
				return "mod-type-playset.svg";

			if (modData.ModCategory.Equals("CostumeCoin", System.StringComparison.InvariantCultureIgnoreCase))
				return "mod-type-costumecoin.svg";

			if (modData.ModCategory.Equals("PowerDisc", System.StringComparison.InvariantCultureIgnoreCase))
				return "mod-type-powerdisc.svg";

			return string.Empty;
		}

		public ListOption[] GetAvailableReplacementCharacters(string idName)
		{
			var mod = GetMod<CharacterModConfiguration>(idName);
			var takenNames = Settings.InstalledMods.Where(i => i.ModCategory == "Character" && i.Parameters.ContainsKey("ReplacementCharacter")).Select(i => i.Parameters["ReplacementCharacter"]);
			var potentialNamePool = IDNames.Where(n => n.Value.Length == mod.PresentationData.Name.Length && !takenNames.Contains(n.Value));
			
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

		public async Task<InstallInfo> InstallCharacterMod(ModInstallationData modToInstall)
		{
			return await UpdateModConfiguration(modToInstall, true);
		}

		public async Task<InstallInfo> UninstallMod(string idName)
		{
			var modToRemove = Settings.InstalledMods.FirstOrDefault(m => m.ModID == idName);
			return await UpdateModConfiguration(modToRemove, false);
		}

		public void SaveSettings()
		{
			SettingsUtility.SaveSettings(Settings, USER_SETTINGS);
		}

		private async Task<InstallInfo> UpdateModConfiguration(ModInstallationData mod, bool install)
		{
			if (!CheckSteamPathSettings())
				throw new System.Exception("Cannot apply mods if the steam installation path has not been set");

			var configuration = new Configuration() { SteamInstallationPath = Settings.SteamInstallationPath };
			var allMods = new List<ModInstallationData>(Settings.InstalledMods);

			if (install)
				allMods.Add(mod);
			else
				allMods.Remove(mod);

			var gameModifications = allMods.Select(i => new GameModification() { 
				Config = GetMod(i.ModID),
				Parameters = i.Parameters
			}).ToArray();

			var modUtility = new ModUtility(configuration);
			var result = await modUtility.ApplyChanges(gameModifications, false);

			switch (result)
			{
				case InstallationStatus.Success:
					Settings.InstalledMods = allMods;
					break;
				case InstallationStatus.FatalError:
					// We remove all installed mods in this instance (something really bad has happened)
					Settings.InstalledMods.Clear();
					break;
			}

			Settings.FileModifications.Clear();
			Settings.FileModifications.AddRange(modUtility.modifications);

			SaveSettings();

			return new InstallInfo(result, modUtility.conflicts);
		}

		private bool CheckSteamPathSettings()
		{
			return !string.IsNullOrEmpty(Settings.SteamInstallationPath);
		}
	}
}
