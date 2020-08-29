using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using InfinityModFramework.InstallActions;
using InfinityModFramework.Enums;
using InfinityModFramework.Models;
using InfinityModFramework.Common.Logging;
using Newtonsoft.Json;

namespace InfinityModFramework.Utilities
{
	public class ModLoader
	{
		ILogger logger;
		Configuration configuration;

		public ModLoader(Configuration configuration, ILogger logger = null)
		{
			this.configuration = configuration;
			this.logger = logger ?? new ConsoleLogger();
		}

		public BaseModConfiguration[] LoadMods(List<string> modsToLoad, List<ModLoadResult> results)
		{
			DeleteAndRecreateFolder(configuration.ModCacheFolder);

			var mods = new List<BaseModConfiguration>();
			results.Clear();

			foreach (var modPath in modsToLoad)
			{
				var errors = new List<string>();
				var fileInfo = new FileInfo(modPath);
				var loadResult = TryLoadV2Mod(fileInfo, out var modData);

				if (loadResult.status == ModLoadStatus.Success)
				{
					if (mods.Any(m => m.ModID == modData.ModID))
					{
						loadResult = new ModLoadResult(loadResult.modFileName, loadResult.modID, ModLoadStatus.DuplicateID);
					}
					else
					{
						mods.Add(modData);
					}
				}

				results.Add(loadResult);
			}

			return mods.ToArray();
		}

		private ModLoadResult TryLoadV2Mod(FileInfo fileInfo, out BaseModConfiguration modData)
		{
			modData = null;
			string zipFile = null;

			try
			{
				zipFile = Path.ChangeExtension(fileInfo.FullName, ".zip");
				File.Copy(fileInfo.FullName, zipFile);

				if (fileInfo.Extension == ".mod")
				{
					var extractFolder = Path.Combine(configuration.ModCacheFolder, fileInfo.Name);

					System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, extractFolder);

					var configPath = Path.Combine(extractFolder, "config.json");

					if (!File.Exists(configPath))
						return new ModLoadResult(fileInfo.Name, modData?.ModID, ModLoadStatus.NoConfig);

					var fileData = File.ReadAllText(configPath);

					modData = JsonConvert.DeserializeObject<BaseModConfiguration>(fileData, new ModInstallActionConverter());

					if (string.IsNullOrEmpty(modData.ModID))
						return new ModLoadResult(fileInfo.Name, modData?.ModID, ModLoadStatus.ConfigInvalid);

					if (modData.Version < 2)
						return new ModLoadResult(fileInfo.Name, modData?.ModID, ModLoadStatus.UnsupportedVersion);

					if (modData.Version > 2)
						return new ModLoadResult(fileInfo.Name, modData?.ModID, ModLoadStatus.UnsupportedVersion);

					modData.ModCachePath = extractFolder;

					// Temporary work around since we can't appear to load images from %APPDATA%
					var imageFileInfo = new FileInfo(Path.Join(modData.ModCachePath, modData.DisplayImage));
					var imageBytes = File.ReadAllBytes(imageFileInfo.FullName);
					modData.DisplayImageBase64 = $"data:image/{imageFileInfo.Extension};base64," + Convert.ToBase64String(imageBytes);

					var loadErrors = new List<string>();
					var configValid = CheckModConfigurationIsValid(modData, loadErrors);

					return new ModLoadResult(fileInfo.Name, modData?.ModID, configValid ? ModLoadStatus.Success : ModLoadStatus.ConfigInvalid, loadErrors);
				}
				else
				{
					return new ModLoadResult(fileInfo.Name, null, ModLoadStatus.ExtensionInvalid);
				}
			}
			catch (Exception ex)
			{
				logger.Log(ex.ToString(), LogSeverity.Error);
				return new ModLoadResult(fileInfo.Name, modData?.ModID, ModLoadStatus.UnspecifiedFailure);
			}
			finally
			{
				if (!string.IsNullOrWhiteSpace(zipFile))
					File.Delete(zipFile);
			}
		}

		bool CheckModConfigurationIsValid(BaseModConfiguration configuration, List<string> loadErrors)
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

		bool CheckFileMoveAction(MoveFileAction action, List<string> loadErrors)
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

		bool CheckFileCopyAction(CopyFileAction action, List<string> loadErrors)
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

		bool CheckFileDeleteAction(DeleteFilesAction action, List<string> loadErrors)
		{
			// Can only delete files in the game path
			if (!action.TargetFiles.All(f => ValidGameFilePath(f)))
			{
				loadErrors.Add($"FileDelete - {nameof(action.TargetFiles)}: Provided path must be in the [GAME] folder");
				return false;
			}

			return action.TargetFiles.All(s => !string.IsNullOrWhiteSpace(s));
		}

		bool CheckFileReplaceAction(ReplaceFileAction action, List<string> loadErrors)
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

		bool CheckFileWriteAction(WriteToFileAction action, List<string> loadErrors)
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

		bool CheckQuickBMSExtractAction(QuickBMSExtractAction action, List<string> loadErrors)
		{
			// Can only modify files in the game path
			if (!action.TargetFiles.All(f => ValidGameFilePath(f)))
			{
				loadErrors.Add($"QuickBMSExtract - {nameof(action.TargetFiles)}: All provided paths for QuickBMS extraction must be in the [GAME] folder");
				return false;
			}

			return action.TargetFiles != null;
		}

		bool CheckUnluacDecompileAction(UnluacDecompileAction action, List<string> loadErrors)
		{
			// Can only modify files in the game path
			if (!action.TargetFiles.All(f => ValidGameFilePath(f)))
			{
				loadErrors.Add($"UnluacDecompile - {nameof(action.TargetFiles)}: All provided paths for Unluac decompile must be in the [GAME] folder");
				return false;
			}

			return action.TargetFiles != null;
		}

		void DeleteAndRecreateFolder(string path)
		{
			// Ensure that we delete this data if it's been left behind
			if (Directory.Exists(path))
				Directory.Delete(path, true);

			Directory.CreateDirectory(path);
		}

		bool ValidFilePath(string path)
		{
			if (string.IsNullOrEmpty(path))
				return false;

			return ValidModFilePath(path) || ValidGameFilePath(path);
		}

		bool ValidModFilePath(string path)
		{
			return path.StartsWith("[MOD]", StringComparison.InvariantCultureIgnoreCase);
		}

		bool ValidGameFilePath(string path)
		{
			return path.StartsWith("[GAME]", StringComparison.InvariantCultureIgnoreCase);
		}
	}
}
