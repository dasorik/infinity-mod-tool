using InfinityModEngine.Utilities;
using InfinityModEngine.Enums;
using InfinityModEngine.Models;
using InfinityModTool.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InfinityModEngine;
using InfinityModEngine.Common.Logging;
using Newtonsoft.Json;
using System.Reflection;

namespace InfinityModTool.Services
{
	public class ModService
	{
#if DEBUG
		const string TOOL_PATH = "..\\..\\..\\Tools";
		const string INTEGRATION_PATH = "..\\..\\..\\Integrations";
#else
		const string TOOL_PATH = "Tools";
		const string INTEGRATION_PATH = "Integrations";
#endif

		public UserModData Settings = new UserModData();

		public BaseModConfiguration[] AvailableMods;
		public IEnumerable<ModLoadResult> ModLoadResults;

		public bool ModLoadWarningShown = false;
		public ILogger logger = new AppLogger();

		string userSaveDataPath = Path.Combine(Global.APP_DATA_FOLDER, "UserSettings.json");
		string backupFolder = Path.Combine(Global.APP_DATA_FOLDER, "Backup");
		string tempFolder = Path.Combine(Global.APP_DATA_FOLDER, "Temp");
		string modCacheFolder = Path.Combine(Global.APP_DATA_FOLDER, "ModCache");
		string modFolder = Path.Combine(Global.APP_DATA_FOLDER, "Mods");

		public ModService()
		{
			this.Settings = LoadSettings();

			if (Settings.InstalledMods == null)
				Settings.InstalledMods = new List<ModInstallationData>();

			ReloadMods();
		}

		private Configuration GetConfiguration()
		{
			return new Configuration() {
				TargetPath = Settings.SteamInstallationPath,
				Modifications = Settings.FileModifications,
				BackupFolder = backupFolder,
				TempFolder = tempFolder,
				CacheFolder = modCacheFolder,
				ToolPath = TOOL_PATH
			};
		}

		public void ReloadMods()
		{
			logger.Log("Reloading Mods...", LogSeverity.Info);

			this.ModLoadWarningShown = false;

			var modLoader = new ModLoader(GetConfiguration(), logger);
			var modPaths = Settings.AvailableMods.Select(m => Path.Combine(modFolder, m));

			this.ModLoadResults = modLoader.LoadMods(modPaths.ToArray());
			this.AvailableMods = ModLoadResults.Where(lr => lr.status == ModLoadStatus.Success).Select(lr => lr.modData).ToArray();

			foreach (var mod in this.ModLoadResults)
			{
				if (mod.loadErrors.Count() == 0)
					continue;

				foreach (var error in mod.loadErrors)
					logger.Log($"{mod.modFileName} - {error}", LogSeverity.Error);
			}

			if (ModLoadResults.All(m => m.status == ModLoadStatus.Success))
				logger.Log("Loaded all mods successfully!", LogSeverity.Info);
		}

		public ModLoadStatus TryAddMod(string fileName, byte[] fileBytes)
		{
			var modInstallPath = Path.Combine(modFolder, fileName);

			if (File.Exists(modInstallPath))
				modInstallPath = FileWriter.GetUniqueFilePath(modInstallPath);

			var fileInfo = new FileInfo(modInstallPath);
			File.WriteAllBytes(modInstallPath, fileBytes);

			// Add this mod to the mod list, so it will attemp to be loaded
			Settings.AvailableMods.Add(fileInfo.Name);

			ReloadMods();

			var result = ModLoadResults.First(r => r.modFileName == fileInfo.Name).status;
			var success = result == ModLoadStatus.Success;

			if (!success)
			{
				// If this mod failed to load, 
				File.Delete(modInstallPath);
				Settings.AvailableMods.Remove(fileInfo.Name);
				ReloadMods();
			}
			else
			{
				SaveSettings();
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

			if (modData.ModCategory.Equals("Misc", System.StringComparison.InvariantCultureIgnoreCase))
				return "mod-type-misc.svg";

			return string.Empty;
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

		public async Task<ModInstallResult> InstallMod(ModInstallationData modToInstall, bool ignoreWarnings)
		{
			return await UpdateModConfiguration(modToInstall, true, ignoreWarnings);
		}

		public async Task<ModInstallResult> UninstallMod(string idName, bool ignoreWarnings)
		{
			var modToRemove = Settings.InstalledMods.FirstOrDefault(m => m.ModID == idName);
			return await UpdateModConfiguration(modToRemove, false, ignoreWarnings);
		}

		public UserModData LoadSettings()
		{
			logger.Log($"Saving user settings to {userSaveDataPath}", LogSeverity.Info);
			return SettingsUtility.LoadSettings<UserModData>(userSaveDataPath);
		}

		public void SaveSettings()
		{
			logger.Log($"Saving user settings to {userSaveDataPath}", LogSeverity.Info);
			SettingsUtility.SaveSettings(Settings, userSaveDataPath);
		}

		private async Task<ModInstallResult> UpdateModConfiguration(ModInstallationData mod, bool install, bool ignoreWarnings)
		{
			if (!CheckSteamPathSettings())
				throw new System.Exception("Cannot apply mods if the steam installation path has not been set");

			var allMods = new List<ModInstallationData>(Settings.InstalledMods);

			if (install)
				allMods.Add(mod);
			else
				allMods.Remove(mod);

			var gameModifications = allMods.Select(i => new ModInstallationInfo() { 
				Config = GetMod(i.ModID),
				Parameters = i.Parameters
			}).ToArray();

			// TODO: Load this differently going forward
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var integrationPath = Path.Combine(executionPath, INTEGRATION_PATH, "disney-infinity.config.json");
			var integrationData = File.ReadAllText(integrationPath);

			var integration = JsonConvert.DeserializeObject<GameIntegration>(integrationData, new ModInstallActionConverter());
			var modUtility = new ModInstaller(GetConfiguration(), integration);
			var result = await modUtility.ApplyChanges(gameModifications, ignoreWarnings);

			switch (result.status)
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
			Settings.FileModifications.AddRange(result.fileModifications);

			SaveSettings();

			return result;
		}

		public void DeleteMod(string modID)
		{
			var loadedMod = ModLoadResults.First(m => m.modData?.ModID == modID);
			
			File.Delete(Path.Combine(modFolder, loadedMod.modFileName));

			Settings.AvailableMods.Remove(loadedMod.modFileName);
			ReloadMods();

			SaveSettings();
		}

		private bool CheckSteamPathSettings()
		{
			return !string.IsNullOrEmpty(Settings.SteamInstallationPath);
		}
	}
}
