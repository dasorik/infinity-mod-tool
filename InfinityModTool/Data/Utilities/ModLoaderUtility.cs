﻿using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System;
using InfinityModTool.Data.InstallActions;
using InfinityModTool.Enums;
using InfinityModTool.Pages;
using InfinityModTool.Utilities;

namespace InfinityModTool.Data.Utilities
{
	public class ModLoaderUtility
	{
		private class ModPathInfo
		{
			public string modPath;
			public string extractBasePath;
		}

		public class ModLoadResult
		{
			public readonly string modFileName;
			public readonly string modID;
			public readonly ModLoadStatus status;
			public readonly IEnumerable<string> loadErrors;

			public ModLoadResult(string modFileName, string modID, ModLoadStatus status, IEnumerable<string> loadErrors = null)
			{
				this.modFileName = modFileName;
				this.modID = modID;
				this.status = status;
				this.loadErrors = loadErrors ?? new List<string>();
			}
		}

		public static string GetModPath()
		{
			return Path.Combine(Global.APP_DATA_FOLDER, "Mods");
		}

		public static void DeleteMod(ModLoadResult result)
		{
			var modPath = Path.Combine(GetModPath(), result.modFileName);
			File.Delete(modPath);
		}

		public static BaseModConfiguration[] LoadMods(List<string> modsToLoad, List<ModLoadResult> allResults)
		{
			var modPath = Path.Combine(Global.APP_DATA_FOLDER, "Mods");
			var extractBasePath = Path.Combine(Global.APP_DATA_FOLDER, "Temp");

			var pathInfo = new ModPathInfo()
			{
				modPath = modPath,
				extractBasePath = extractBasePath
			};
			
			if (!Directory.Exists(modPath))
				Directory.CreateDirectory(modPath);

			DeleteAndRecreateFolder(extractBasePath);

			var mods = new List<BaseModConfiguration>();
			allResults.Clear();

			foreach (var modName in modsToLoad)
			{
				var errors = new List<string>();
				var fileInfo = new FileInfo(Path.Combine(modPath, modName));
				var result = TryLoadV1Mod(fileInfo, pathInfo, out var modData, errors);

				if (result == ModLoadStatus.Success)
				{
					if (mods.Any(m => m.ModID == modData.ModID))
					{
						result = ModLoadStatus.DuplicateID;
					}
					else
					{
						mods.Add(modData);
					}
				}

				allResults.Add(new ModLoadResult(fileInfo.Name, result == ModLoadStatus.Success ? modData?.ModID : null, result, errors));
			}

			return mods.ToArray();
		}

		private static ModLoadStatus TryLoadV1Mod(FileInfo fileInfo, ModPathInfo pathInfo, out BaseModConfiguration modData, List<string> loadErrors)
		{
			modData = null;
			string zipFile = null;

			try
			{
				zipFile = Path.ChangeExtension(fileInfo.FullName, ".zip");
				File.Copy(fileInfo.FullName, zipFile);

				if (fileInfo.Extension == ".mod")
				{
					var extractFolder = Path.Combine(pathInfo.extractBasePath, fileInfo.Name);

					System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, extractFolder);

					var configPath = Path.Combine(extractFolder, "config.json");

					if (!File.Exists(configPath))
						return ModLoadStatus.NoConfig;

					var fileData = File.ReadAllText(configPath);
					//JsonMapper.ToObject<BaseModConfiguration>(fileData);
					modData = Newtonsoft.Json.JsonConvert.DeserializeObject<BaseModConfiguration>(fileData, new ModInstallActionConverter());

					if (string.IsNullOrEmpty(modData.ModID))
						return ModLoadStatus.ConfigInvalid;

					if (modData.Version < 2)
						return ModLoadStatus.UnsupportedVersion;

					if (modData.Version > 2)
						return ModLoadStatus.UnsupportedVersion;

					modData = LoadModData(modData, extractFolder);
					modData.ModCachePath = extractFolder;

					// Temporary work around since we can't appear to load images from %APPDATA%
					var imageFileInfo = new FileInfo(Path.Join(modData.ModCachePath, modData.DisplayImage));
					var imageBytes = File.ReadAllBytes(imageFileInfo.FullName);
					modData.DisplayImageBase64 = $"data:image/{imageFileInfo.Extension};base64," + Convert.ToBase64String(imageBytes);

					var configValid = CheckModConfigurationIsValid(modData, loadErrors);

					return configValid ? ModLoadStatus.Success : ModLoadStatus.ConfigInvalid;
				}
				else
				{
					return ModLoadStatus.ExtensionInvalid;
				}
			}
			catch (Exception ex)
			{
				Logging.LogMessage(ex.ToString(), Logging.LogSeverity.Error);
				return ModLoadStatus.UnspecifiedFailure;
			}
			finally
			{
				if (!string.IsNullOrWhiteSpace(zipFile))
					File.Delete(zipFile);
			}
		}

		static bool CheckModConfigurationIsValid(BaseModConfiguration configuration, List<string> loadErrors)
		{
			bool result = true;

			// If we don't have any actions, no need to validate
			if (configuration.InstallActions == null)
				return true;

			foreach (var action in configuration.InstallActions)
			{
				if (action is MoveFileAction)
					result &= CheckFileMoveAction(action as MoveFileAction, loadErrors);
				else if (action is CopyFileAction)
					result &= CheckFileCopyAction(action as CopyFileAction, loadErrors);
				else if (action is DeleteFilesAction)
					result &= CheckFileDeleteAction(action as DeleteFilesAction, loadErrors);
				else if (action is ReplaceFileAction)
					result &= CheckFileReplaceAction(action as ReplaceFileAction, loadErrors);
				else if (action is WriteToFileAction)
					result &= CheckFileWriteAction(action as WriteToFileAction, loadErrors);
				else if (action is QuickBMSExtractAction)
					result &= CheckQuickBMSExtractAction(action as QuickBMSExtractAction, loadErrors);
				else if (action is UnluacDecompileAction)
					result &= CheckUnluacDecompileAction(action as UnluacDecompileAction, loadErrors);
			}

			return result;
		}

		static bool CheckFileMoveAction(MoveFileAction action, List<string> loadErrors)
		{
			// Files can only be moved into the game folder
			if (!ValidGameFilePath(action.DestinationPath))
			{
				loadErrors.Add($"FileMove - {nameof(action.DestinationPath)}: Provided path must be in the [GAME] folder");
				return false;
			}

			// Files can only be moved from the game folder
			if (!ValidGameFilePath(action.TargetFile))
			{
				loadErrors.Add($"FileMove - {nameof(action.TargetFile)}: Provided path must be in the [GAME] folder");
				return false;
			}

			return !string.IsNullOrWhiteSpace(action.TargetFile) && !string.IsNullOrWhiteSpace(action.DestinationPath);
		}

		static bool CheckFileCopyAction(CopyFileAction action, List<string> loadErrors)
		{
			// Files can only be copied into the game folder
			if (!ValidGameFilePath(action.DestinationPath))
			{
				loadErrors.Add($"FileCopy - {nameof(action.DestinationPath)}: Provided path must be in the [GAME] folder");
				return false;
			}

			// Target file can be either in game or mod path
			if (!ValidFilePath(action.TargetFile))
			{
				loadErrors.Add($"FileCopy - {nameof(action.TargetFile)}: Provided path must be in the [GAME] or [MOD] folder");
				return false;
			}

			return !string.IsNullOrWhiteSpace(action.TargetFile) && !string.IsNullOrWhiteSpace(action.DestinationPath);
		}

		static bool CheckFileDeleteAction(DeleteFilesAction action, List<string> loadErrors)
		{
			// Can only delete files in the game path
			if (!action.TargetFiles.All(f => ValidGameFilePath(f)))
			{
				loadErrors.Add($"FileDelete - {nameof(action.TargetFiles)}: Provided path must be in the [GAME] folder");
				return false;
			}

			return action.TargetFiles.All(s => !string.IsNullOrWhiteSpace(s));
		}

		static bool CheckFileReplaceAction(ReplaceFileAction action, List<string> loadErrors)
		{
			// The replacement file MUST be from the mods folder
			if (!ValidModFilePath(action.ReplacementFile))
			{
				loadErrors.Add($"FileReplace - {nameof(action.ReplacementFile)}: Provided path must be in the [MOD] folder");
				return false;
			}

			// Target file can be either in game or mod path
			if (!ValidFilePath(action.TargetFile))
			{
				loadErrors.Add($"FileReplace - {nameof(action.TargetFile)}: Provided path must be in the [GAME] folder");
				return false;
			}

			return !string.IsNullOrWhiteSpace(action.TargetFile) && !string.IsNullOrWhiteSpace(action.ReplacementFile);
		}

		static bool CheckFileWriteAction(WriteToFileAction action, List<string> loadErrors)
		{
			if (action.Content is null)
			{
				loadErrors.Add($"FileWrite - {nameof(action.Content)}: No items provided in 'Content' list");
				return false;
			}

			// Can only modify files in the game path
			if (!ValidGameFilePath(action.TargetFile))
			{
				loadErrors.Add($"FileWrite - {nameof(action.TargetFile)}: Provided path must be in the [GAME] folder");
				return false;
			}

			foreach (var content in action.Content)
			{
				if (!string.IsNullOrWhiteSpace(content.DataFilePath) && !string.IsNullOrWhiteSpace(content.Text))
				{
					loadErrors.Add($"FileWrite - {nameof(content.DataFilePath)}/{nameof(content.Text)}: File write action must provide either 'Text' or 'DataFilePath' properties");
					return false;
				}

				if (content.Replace && !content.EndOffset.HasValue)
				{
					loadErrors.Add($"FileWrite - {nameof(content.Replace)}: File writes with 'Replace' enabled must provide an 'EndOffset' property");
					return false;
				}
			}

			return true;
		}

		static bool CheckQuickBMSExtractAction(QuickBMSExtractAction action, List<string> loadErrors)
		{
			// Can only modify files in the game path
			if (!action.TargetFiles.All(f => ValidGameFilePath(f)))
			{
				loadErrors.Add($"QuickBMSExtract - {nameof(action.TargetFiles)}: All provided paths for QuickBMS extraction must be in the [GAME] folder");
				return false;
			}

			return action.TargetFiles != null;
		}

		static bool CheckUnluacDecompileAction(UnluacDecompileAction action, List<string> loadErrors)
		{
			// Can only modify files in the game path
			if (!action.TargetFiles.All(f => ValidGameFilePath(f)))
			{
				loadErrors.Add($"UnluacDecompile - {nameof(action.TargetFiles)}: All provided paths for Unluac decompile must be in the [GAME] folder");
				return false;
			}

			return action.TargetFiles != null;
		}

		static BaseModConfiguration LoadModData(BaseModConfiguration configuration, string extractPath)
		{
			if (configuration.Version < 2)
			{
				switch (configuration.ModCategory)
				{
					case "Character":
						return LoadCharacterModData(configuration, extractPath);
					case "CostumeCoin":
						return LoadCostumeCoinModData(configuration, extractPath);
					case "Playset":
						return LoadPlaysetModData(configuration, extractPath);
					default:
						return configuration;
				}
			}

			return configuration;
		}

		static BaseModConfiguration LoadCharacterModData(BaseModConfiguration configuration, string extractPath)
		{
			var configPath = Path.Combine(extractPath, "config.json");
			var presentationPath = Path.Combine(extractPath, "presentation.json");

			var modData = Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterModConfiguration>(File.ReadAllText(configPath));
			var presentationData = Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterData>(File.ReadAllText(presentationPath));

			modData.PresentationData = presentationData;
			return modData;
		}

		static CostumeCoinModConfiguration LoadCostumeCoinModData(BaseModConfiguration configuration, string extractPath)
		{
			var configPath = Path.Combine(extractPath, "config.json");
			var presentationPath = Path.Combine(extractPath, "presentation.json");

			var modData = Newtonsoft.Json.JsonConvert.DeserializeObject<CostumeCoinModConfiguration>(File.ReadAllText(configPath));
			var presentationData = Newtonsoft.Json.JsonConvert.DeserializeObject<CostumeCoinData>(File.ReadAllText(presentationPath));

			modData.PresentationData = presentationData;
			return modData;
		}

		static PlaysetModConfiguration LoadPlaysetModData(BaseModConfiguration configuration, string extractPath)
		{
			var configPath = Path.Combine(extractPath, "config.json");
			var modData = Newtonsoft.Json.JsonConvert.DeserializeObject<PlaysetModConfiguration>(File.ReadAllText(configPath));
			return modData;
		}

		static void DeleteAndRecreateFolder(string path)
		{
			// Ensure that we delete this data if it's been left behind
			if (Directory.Exists(path))
				Directory.Delete(path, true);

			Directory.CreateDirectory(path);
		}

		static bool ValidFilePath(string path)
		{
			if (string.IsNullOrEmpty(path))
				return false;

			return ValidModFilePath(path) || ValidGameFilePath(path);
		}

		static bool ValidModFilePath(string path)
		{
			return path.StartsWith("[MOD]", StringComparison.InvariantCultureIgnoreCase);
		}

		static bool ValidGameFilePath(string path)
		{
			return path.StartsWith("[GAME]", StringComparison.InvariantCultureIgnoreCase);
		}
	}
}
