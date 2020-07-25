using InfinityModTool.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using InfinityModTool.Data.Modifications;
using InfinityModTool.Extension;
using InfinityModTool.Data.InstallActions;
using System.Reflection;
using InfinityModTool.Enums;
using InfinityModTool.Models;

namespace InfinityModTool.Utilities
{
	public class ModUtility
	{
		public class ReservedFileModificationException : Exception
		{
			public ReservedFileModificationException(string message) : base(message) { }
		}

		public class InstallInfo
		{
			public readonly InstallationStatus status;
			public readonly ModCollision[] conflicts;

			public InstallInfo(InstallationStatus status)
				: this(status, new ModCollision[0]) { }

			public InstallInfo(InstallationStatus status, ModCollision[] conflicts)
			{
				this.status = status;
				this.conflicts = conflicts;
			}
		}

		public static async Task<InstallInfo> ApplyChanges(Configuration configuration, GameModification[] mods, bool ignoreWarnings)
		{
			return await ApplyChangesInternal(configuration, mods, ignoreWarnings, false);
		}
		
		public static async Task<InstallInfo> ApplyChangesInternal(Configuration configuration, GameModification[] mods, bool ignoreWarnings, bool reverting)
		{
			var tempFolder = CreateTempFolder();

			// Start with a blank canvas
			RemoveAllChanges(configuration);

			if (mods.Length == 0)
				return new InstallInfo(InstallationStatus.Success);

			try
			{
				var modActions = GetModActions(mods);
				var collisionTracker = new ModCollisionTracker();

				var conflicts = collisionTracker.CheckForPotentialModCollisions(mods.Last(), modActions);

				if (conflicts.Length > 0)
					return new InstallInfo(conflicts.Any(c => c.severity == ModCollisionSeverity.Clash) ? InstallationStatus.UnresolvableConflict : InstallationStatus.ResolvableConflict, conflicts);

				await ExtractFiles(configuration, tempFolder);
				
				var virtualreaderFile = Path.Combine(tempFolder, "presentation", "virtualreaderpc_data.lua");
				var virtualreaderDecompiledFile = Path.Combine(tempFolder, "virtualreaderpc_data_decomp.lua");
				var playsetlistwin32File = Path.Combine(tempFolder, "presentation", "playsetlistwin32.lua");
				var playsetlistwin32DecompiledFile = Path.Combine(tempFolder, "playsetlistwin32_decomp.lua");
				var igpLocksFile = Path.Combine(tempFolder, "igp", "locks__lua.chd");

				await UnluacUtility.Decompile(virtualreaderFile, virtualreaderDecompiledFile);
				await UnluacUtility.Decompile(playsetlistwin32File, playsetlistwin32DecompiledFile);

				// Get mod types
				var characterMods = mods.Where(m => m.Config.ModCategory.SafeEquals("Character"));
				var costumeCoinMods = mods.Where(m => m.Config.ModCategory.SafeEquals("CostumeCoin"));
				var playsetMods = mods.Where(m => m.Config.ModCategory.SafeEquals("Playset"));

				var characterJson = GenerateCharacterJson(characterMods.Select(m => m.GetConfig<CharacterModConfiguration>()));
				var costumeCoinJson = GenerateCostumeCoinJson(costumeCoinMods.Select(m => m.GetConfig<CostumeCoinModConfiguration>()));

				// Write any data as required to the virtualreader file, make sure to offset by bytesWritten if needed
				var fileWriter = new FileWriterUtility();

				fileWriter.WriteToFile(virtualreaderDecompiledFile, characterJson, 0x0000A5C6, true, false);
				fileWriter.WriteToFile(virtualreaderDecompiledFile, costumeCoinJson, 0x0001588F + 1, true, false);

				// Apply character specific actions
				foreach (var mod in characterMods)
				{
					if (ModRequiresReplacement(mod))
					{
						var offset = FindReplacementOffset(igpLocksFile, mod.Parameters["ReplacementCharacter"]);
						fileWriter.WriteToFile(igpLocksFile, mod.GetConfig<CharacterModConfiguration>().PresentationData.Name, offset, false, true);
					}
				}

				// Apply costume coin specific actions
				foreach (var mod in costumeCoinMods)
				{
					var config = mod.GetConfig<CostumeCoinModConfiguration>();

					if (config.WriteToCharacterList)
					{
						long startIndex, endIndex;
						var characterData = GetCharacterDataFromFile(virtualreaderDecompiledFile, config.TargetCharacterID, out startIndex, out endIndex);
						characterData.CostumeCoin = config.PresentationData.Name;

						fileWriter.WriteToFileRange(virtualreaderDecompiledFile, GenerateCharacterJson(characterData), startIndex, endIndex, true);
					}
				}

				// Unlock playsets
				foreach (var mod in playsetMods)
				{
					var config = mod.GetConfig<PlaysetModConfiguration>();
					AddPlayset(config, playsetlistwin32DecompiledFile, fileWriter);
				}

				var extractedFiles = new List<string>();
				var decompiledFiles = new List<string>();

				foreach (var extractAction in modActions.extractActions)
					await QuickBMSExtract(extractAction, tempFolder, extractedFiles, configuration);

				foreach (var decompileAction in modActions.decompileActions)
					await UnluacDecompile(decompileAction, tempFolder, decompiledFiles, configuration);

				foreach (var fileCopyAction in modActions.fileCopyActions)
					CopyFile(fileCopyAction, configuration);

				foreach (var fileReplaceAction in modActions.fileReplaceActions)
					ReplaceFile(fileReplaceAction, configuration);

				foreach (var fileWriteAction in modActions.fileWriteActions)
					WriteToFile(fileWriteAction, fileWriter, configuration);

				foreach (var fileMoveAction in modActions.fileMoveActions)
					MoveFile(fileMoveAction, configuration);

				foreach (var fileDeleteAction in modActions.fileDeleteActions)
					DeleteFile(fileDeleteAction, configuration);
			}
			catch (Exception ex)
			{
				Directory.Delete(tempFolder, true);

				if (reverting)
				{
					// If we error out during revert, delete everything (something has gone badly wrong)
					RemoveAllChanges(configuration);
					return new InstallInfo(InstallationStatus.FatalError);
				}
				else
				{
					// Revert back to the previous install state
					await ApplyChangesInternal(configuration, mods.Take(mods.Count() - 1).ToArray(), true, true);
					return new InstallInfo(InstallationStatus.RolledBackError);
				}
			}

			CopyFiles(configuration, tempFolder);
			Directory.Delete(tempFolder, true);

			return new InstallInfo(InstallationStatus.Success);
		}

		public static ModActionCollection GetModActions(GameModification[] mods)
		{
			// Custom actions
			var extractActions = new List<ModAction<QuickBMSExtractAction>>();
			var decompileActions = new List<ModAction<UnluacDecompileAction>>();
			var fileMoveActions = new List<ModAction<FileMoveAction>>();
			var fileDeleteActions = new List<ModAction<FileDeleteAction>>();
			var fileWriteActions = new List<ModAction<FileWriteAction>>();
			var fileReplaceActions = new List<ModAction<FileReplaceAction>>();
			var fileCopyActions = new List<ModAction<FileCopyAction>>();

			// We need to collate these steps, so we can minimize collision issues
			foreach (var mod in mods)
			{
				var installActions = (mod.Config.InstallActions ?? new ModInstallAction[] { });

				extractActions.AddRange(installActions.Where(a => a.Action.SafeEquals("QuickBMSExtract", ignoreCase: true)).Select(a => new ModAction<QuickBMSExtractAction>(mod, a as QuickBMSExtractAction)));
				decompileActions.AddRange(installActions.Where(a => a.Action.SafeEquals("UnluacDecompile", ignoreCase: true)).Select(a => new ModAction<UnluacDecompileAction>(mod, a as UnluacDecompileAction)));
				fileMoveActions.AddRange(installActions.Where(a => a.Action.SafeEquals("MoveFile", ignoreCase: true)).Select(a => new ModAction<FileMoveAction>(mod, a as FileMoveAction)));
				fileDeleteActions.AddRange(installActions.Where(a => a.Action.SafeEquals("DeleteFile", ignoreCase: true)).Select(a => new ModAction<FileDeleteAction>(mod, a as FileDeleteAction)));
				fileWriteActions.AddRange(installActions.Where(a => a.Action.SafeEquals("WriteToFile", ignoreCase: true)).Select(a => new ModAction<FileWriteAction>(mod, a as FileWriteAction)));
				fileReplaceActions.AddRange(installActions.Where(a => a.Action.SafeEquals("ReplaceFile", ignoreCase: true)).Select(a => new ModAction<FileReplaceAction>(mod, a as FileReplaceAction)));
				fileCopyActions.AddRange(installActions.Where(a => a.Action.SafeEquals("CopyFile", ignoreCase: true)).Select(a => new ModAction<FileCopyAction>(mod, a as FileCopyAction)));
			}

			return new ModActionCollection()
			{
				extractActions = extractActions,
				decompileActions = decompileActions,
				fileMoveActions = fileMoveActions,
				fileDeleteActions = fileDeleteActions,
				fileWriteActions = fileWriteActions,
				fileReplaceActions = fileReplaceActions,
				fileCopyActions = fileCopyActions
			};
		}

		public static string CreateTempFolder()
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var tempDirectory = Path.Combine(executionPath, "Temp");

			if (Directory.Exists(tempDirectory))
				Directory.Delete(tempDirectory, true);

			Directory.CreateDirectory(tempDirectory);

			return tempDirectory;
		}

		private static async Task QuickBMSExtract(ModAction<QuickBMSExtractAction> modAction, string tempFolder, List<string> extractedFiles, Configuration config)
		{
			foreach (var file in modAction.action.TargetFiles)
			{
				var physicalTargetPath = ResolvePath(file, modAction.mod);
				var fileInfo = new FileInfo(physicalTargetPath);

				// Check if this has already been extracted
				if (extractedFiles.Contains(physicalTargetPath))
					continue;

				await QuickBMSUtility.ExtractFiles(physicalTargetPath, tempFolder);

				// Move all extracted files into the root folder
				var newFolder = Path.Combine(tempFolder, Path.GetFileNameWithoutExtension(fileInfo.Name));
				var newFiles = Directory.GetFiles(newFolder);

				foreach (var newFile in newFiles)
					File.Move(newFile, Path.Combine(fileInfo.Directory.FullName, new FileInfo(newFile).Name));

				extractedFiles.Add(physicalTargetPath);
			}
		}

		private static async Task UnluacDecompile(ModAction<UnluacDecompileAction> modAction, string tempFolder, List<string> decompiledFiles, Configuration config)
		{
			foreach (var file in modAction.action.TargetFiles)
			{
				var physicalTargetPath = ResolvePath(file, modAction.mod);
				var fileInfo = new FileInfo(physicalTargetPath);

				var targetPath = Path.Combine(tempFolder, $"{Path.GetFileNameWithoutExtension(fileInfo.Name)}_decomp{fileInfo.Extension}");

				// Check if this has already been decompiled
				if (decompiledFiles.Contains(physicalTargetPath))
					continue;

				// Decompile the file, and replace the file
				await UnluacUtility.Decompile(physicalTargetPath, targetPath);
				File.Move($"{physicalTargetPath}_decomp", physicalTargetPath, true);

				decompiledFiles.Add(physicalTargetPath);
			}
		}

		private static void MoveFile(ModAction<FileMoveAction> modAction, Configuration config)
		{
			var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod);

			if (!IsReservedFile(physicalTargetPath, config) && !IsReservedFile(physicalDestinationPath, config))
				File.Move(physicalTargetPath, physicalDestinationPath, false);
			else
				throw new ReservedFileModificationException("Cannot move a reserved file");
		}

		private static void ReplaceFile(ModAction<FileReplaceAction> modAction, Configuration config)
		{
			var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod);
			var physicalDestinationPath = ResolvePath(modAction.action.ReplacementFile, modAction.mod);

			if (!IsReservedFile(physicalTargetPath, config) && !IsReservedFile(physicalDestinationPath, config))
				File.Move(physicalTargetPath, physicalDestinationPath, true);
			else
				throw new ReservedFileModificationException("Cannot replace a reserved file");
		}

		private static void CopyFile(ModAction<FileCopyAction> modAction, Configuration config)
		{
			var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod);

			File.Copy(physicalTargetPath, physicalDestinationPath, false);
		}

		private static void DeleteFile(ModAction<FileDeleteAction> modAction, Configuration config)
		{
			var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod);

			if (!IsReservedFile(physicalTargetPath, config))
				File.Delete(physicalTargetPath);
			else
				throw new ReservedFileModificationException("Cannot delete a reserved file");
		}

		private static void WriteToFile(ModAction<FileWriteAction> modAction, FileWriterUtility fileWriter, Configuration config)
		{
			foreach (var content in modAction.action.Content)
			{
				var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod);
				string filePath = null;

				if (!string.IsNullOrEmpty(content.DataFilePath))
					filePath = ResolvePath(content.DataFilePath, modAction.mod);

				var dataToWrite = content.Text ?? File.ReadAllText(filePath);

				if (!IsReservedFile(physicalTargetPath, config))
				{
					if (content.EndOffset.HasValue)
						fileWriter.WriteToFileRange(filePath, dataToWrite, content.StartOffset, content.EndOffset.Value, false);
					else
						fileWriter.WriteToFile(physicalTargetPath, dataToWrite, content.StartOffset, !content.Replace, false);
				}
				else
					throw new ReservedFileModificationException("Cannot write to a reserved file");
			}
		}

		private static bool IsReservedFile(string path, Configuration config)
		{
			return path.StartsWith(config.SteamInstallationPath, StringComparison.InvariantCultureIgnoreCase) && new FileInfo(path).Extension == ".zip";
		}

		public static string ResolvePath(string path, GameModification mod)
		{
			if (string.IsNullOrEmpty(path))
				return null;

			if (path.StartsWith("~"))
				return Path.Combine(mod.Config.ModCachePath, path.Substring(1, path.Length - 1));
			else if (path.StartsWith("[GAME]"))
				return Path.Combine(mod.Config.ModCachePath, path.Substring(6, path.Length - 6));

			if (path.StartsWith("@"))
				return Path.Combine(mod.Config.ModCachePath, path.Substring(1, path.Length - 1));
			else if (path.StartsWith("[MOD]"))
				return Path.Combine(mod.Config.ModCachePath, path.Substring(5, path.Length - 5));

			throw new Exception("Supplied path must begin with the following: [GAME], [MOD], ~ or @");
		}

		private static async Task ExtractFiles(Configuration configuration, string tempFolder)
		{
			var presentationZip = Path.Combine(configuration.SteamInstallationPath, "assets", "presentation", "presentation.zip");
			var igpZip = Path.Combine(configuration.SteamInstallationPath, "assets", "gamedb", "igp", "igp.zip");

			var presentationBackupZip = Path.Combine(configuration.SteamInstallationPath, "assets", "presentation", "presentation_bak.zip");
			var igpBackupZip = Path.Combine(configuration.SteamInstallationPath, "assets", "gamedb", "igp", "igp_bak.zip");

			// In the instance we've already extracted these before, we will have renamed them
			if (!File.Exists(presentationZip))
				presentationZip = presentationBackupZip;

			if (!File.Exists(igpZip))
				igpZip = igpBackupZip;

			await QuickBMSUtility.ExtractFiles(presentationZip, tempFolder);
			await QuickBMSUtility.ExtractFiles(igpZip, tempFolder);

			// Only applicable if we haven't already changed the filename
			File.Move(presentationZip, presentationBackupZip);
			File.Move(igpZip, igpBackupZip);
		}

		public static void RemoveAllChanges(Configuration configuration)
		{
			var presentationZip = Path.Combine(configuration.SteamInstallationPath, "assets", "presentation", "presentation.zip");
			var igpZip = Path.Combine(configuration.SteamInstallationPath, "assets", "gamedb", "igp", "igp.zip");

			var presentationBackupZip = Path.Combine(configuration.SteamInstallationPath, "assets", "presentation", "presentation_bak.zip");
			var igpBackupZip = Path.Combine(configuration.SteamInstallationPath, "assets", "gamedb", "igp", "igp_bak.zip");

			// In the instance we've already extracted these before, we will have renamed them
			if (File.Exists(presentationBackupZip))
				File.Move(presentationBackupZip, presentationZip);

			if (File.Exists(igpBackupZip))
				File.Move(igpBackupZip, igpZip);

			var presentationFolder = Path.Combine(configuration.SteamInstallationPath, "assets", "presentation");
			var igpFolder = Path.Combine(configuration.SteamInstallationPath, "assets", "gamedb", "igp");
			var flashFolder = Path.Combine(configuration.SteamInstallationPath, "assets", "flash");

			DeleteFromFolder(presentationFolder, ".lua");
			DeleteFromFolder(igpFolder, ".lua", ".chd");
			DeleteFromFolder(flashFolder, ".gfx");
		}

		private static string GenerateCharacterJson(IEnumerable<CharacterModConfiguration> mods)
		{
			var presentationDataToWrite = mods.Where(m => m.WriteToCharacterList);
			var jsonWriter = new StringBuilder();

			foreach (var character in presentationDataToWrite)
			{
				var characterJson = GenerateCharacterJson(character.PresentationData, prependComma: true);
				jsonWriter.Append(characterJson);
			}

			return jsonWriter.ToString();
		}

		private static string GenerateCostumeCoinJson(IEnumerable<CostumeCoinModConfiguration> mods)
		{
			var presentationDataToWrite = mods.Where(m => m.WriteToCostumeCoinList);
			var jsonWriter = new StringBuilder();

			foreach (var costumeCoin in presentationDataToWrite)
			{
				var characterJson = GenerateCostumeCoinJson(costumeCoin.PresentationData, prependComma: true);
				jsonWriter.Append(characterJson);
			}

			return jsonWriter.ToString();
		}

		private static string GenerateCharacterJson(CharacterData data, bool prependComma = false)
		{
			var sb = new StringBuilder();

			if (prependComma)
				sb.Append(",\r\n  ");

			sb.Append("{");
			sb.Append($"\r\n    Name = \"{data.Name}\",");
			sb.Append($"\r\n    sku_id = \"{data.Sku_Id}\",");
			sb.Append($"\r\n    SteamDLCAppId = \"{data.SteamDLCAppId}\",");
			sb.Append($"\r\n    PCSKU = \"{data.PCSKU}\",");
			sb.Append($"\r\n    WINRTSKU = \"{data.WINRTSKU}\",");
			sb.Append($"\r\n    Icon = \"{data.Icon}\",");
			sb.Append($"\r\n    Description = \"{data.Description}\",");
			sb.Append($"\r\n    VideoLink = \"{data.VideoLink}\",");
			sb.Append($"\r\n    ProgressionTree = \"{data.ProgressionTree}\",");
			sb.Append($"\r\n    CostumeCoin = \"{data.CostumeCoin}\",");
			sb.Append($"\r\n    MetaData = \"{data.MetaData}\"");
			sb.Append("\r\n  }");

			return sb.ToString();
		}

		private static string GenerateCostumeCoinJson(CostumeCoinData data, bool prependComma = false)
		{
			var sb = new StringBuilder();

			if (prependComma)
				sb.Append(",\r\n  ");

			sb.Append("{");
			sb.Append($"\r\n    sku_id = \"{data.Sku_Id}\",");
			sb.Append($"\r\n    Name = \"{data.Name}\",");
			sb.Append($"\r\n    CoinType = \"{data.CoinType}\",");
			sb.Append($"\r\n    SparkCost = \"{data.SparkCost}\",");
			sb.Append($"\r\n    MetaData = \"{data.MetaData}\"");
			sb.Append("\r\n  }");

			return sb.ToString();
		}

		private static CharacterData GetCharacterDataFromFile(string filePath, string targetID, out long startOffset, out long endOffset)
		{
			string dataPattern = @"^{.+}$";
			string propertyPattern = @"\s{4}([a-zA-Z_]+) = \""(.*)\""";

			byte[] startSignature = new byte[] { 123, 13, 10 }; // {..
			byte[] endSignature = new byte[] { 13, 10, 32, 32, 125 }; // ..}

			startOffset = FindReplacementOffset(filePath, $"    Name = \"{targetID}\"") - startSignature.Length;
			endOffset = FindReplacementOffset(filePath, endSignature, startOffset) + endSignature.Length;

			var fileBytes = File.ReadAllBytes(filePath);
			byte[] truncatedByteBuffer = new byte[endOffset - startOffset];
			Array.Copy(fileBytes, startOffset, truncatedByteBuffer, 0, endOffset - startOffset);

			var truncatedText = Encoding.ASCII.GetString(truncatedByteBuffer);

			if (Regex.IsMatch(truncatedText, dataPattern, RegexOptions.Singleline))
			{
				var matches = Regex.Matches(truncatedText, propertyPattern);
				var properties = new Dictionary<string, string>();

				foreach (Match match in matches)
				{
					var groups = match.Groups;
					properties.Add(groups[1].Value, groups[2].Value);
				}

				var characterData = new CharacterData();
				characterData.Name = properties["Name"];
				characterData.Sku_Id = properties["sku_id"];
				characterData.SteamDLCAppId = properties["SteamDLCAppId"];
				characterData.PCSKU = properties["PCSKU"];
				characterData.WINRTSKU = properties["WINRTSKU"];
				characterData.Icon = properties["Icon"];
				characterData.Description = properties["Description"];
				characterData.VideoLink = properties["VideoLink"];
				characterData.ProgressionTree = properties["ProgressionTree"];
				characterData.CostumeCoin = properties["CostumeCoin"];
				characterData.MetaData = properties["MetaData"];

				return characterData;
			}

			return null;
		}

		private static long FindReplacementOffset(string lockFile, string replacementName, long? startFromIndex = null)
		{
			var replacementNameBytes = Encoding.UTF8.GetBytes(replacementName);
			return FindReplacementOffset(lockFile, replacementNameBytes, startFromIndex);
		}

		private static long FindReplacementOffset(string lockFile, byte[] searchBytes, long? startFromIndex = null)
		{
			var fileBytes = File.ReadAllBytes(lockFile);

			int subSearchLength = searchBytes.Length;
			bool match = false;
			long i, j;

			for (i = startFromIndex ?? 0; i < fileBytes.LongLength; i++)
			{
				match = true;

				for (j = 0; j < subSearchLength; j++)
				{
					if (fileBytes[i + j] != searchBytes[j])
					{
						match = false;
						break;
					}
				}

				if (match)
					return i;
			}

			throw new System.Exception($"Unable to find the string '{Encoding.UTF8.GetString(searchBytes)}' within the requested file");
		}

		private static void CopyFiles(Configuration configuration, string tempFolder)
		{
			var presentationFolder = Path.Combine(configuration.SteamInstallationPath, "assets", "presentation");
			var igpFolder = Path.Combine(configuration.SteamInstallationPath, "assets", "gamedb", "igp");
			var flashFolder = Path.Combine(configuration.SteamInstallationPath, "assets", "flash");
			var virtualReaderFile = Path.Combine(presentationFolder, "virtualreaderpc_data.lua");
			var playsetlistwin32File = Path.Combine(presentationFolder, "playsetlistwin32.lua");

			var tempPresentationFolder = Path.Combine(tempFolder, "presentation");
			var tempIgpFolder = Path.Combine(tempFolder, "igp");
			var tempVirtualReaderFile = Path.Combine(tempFolder, "virtualreaderpc_data_decomp.lua");
			var tempPlaysetlistwin32File = Path.Combine(tempFolder, "playsetlistwin32_decomp.lua");
			var tempFlashFile = Path.Combine(tempFolder, "presentation", "flash", "container.gfx");

			// Copy all items from the presentation folder, except for the flash folder
			foreach (var file in Directory.GetFiles(tempPresentationFolder))
				CopyFileIntoFolder(file, presentationFolder, true);

			// Override the virtualreaderpc_data.lua file
			File.Copy(tempVirtualReaderFile, virtualReaderFile, true);

			// Override the playsetlistwin32.lua file
			File.Copy(tempPlaysetlistwin32File, playsetlistwin32File, true);

			// Copy contents into the flash folder
			CopyFileIntoFolder(tempFlashFile, flashFolder, true);

			// Copy contents of igp folder
			foreach (var file in Directory.GetFiles(tempIgpFolder))
				CopyFileIntoFolder(file, igpFolder, true);
		}

		private static bool CopyFileIntoFolder(string filePath, string directory, bool ignoreDirectories)
		{
			var fileInfo = new FileInfo(filePath);
			var fileAttributes = File.GetAttributes(filePath);

			if (fileAttributes.HasFlag(FileAttributes.Directory))
				return false;

			File.Copy(filePath, Path.Combine(directory, fileInfo.Name));
			return true;
		}

		private static void DeleteFromFolder(string directory, params string[] extensionsToDelete)
		{
			foreach (var file in Directory.GetFiles(directory))
			{
				if (extensionsToDelete.Any(e => file.EndsWith(e)))
					File.Delete(file);
			}
		}

		private static bool ModRequiresReplacement(GameModification modification)
		{
			return !string.IsNullOrEmpty(modification.Parameters.GetValueOrDefault("ReplacementCharacter", null));
		}

		private static void AddPlayset(PlaysetModConfiguration config, string playsetListFile, FileWriterUtility fileWriter)
		{
			var injectedCodeUI = $"        self:AddListButton(\"{config.Name}\")\r\n";
			var injectedCodeAdd = $"    self:AddListButton(\"{config.Name}\")\r\n";
			var injectedCodeOr = $" or t.id ~= \"{config.Name}\"";
			var injectedCodeAnd = $" and t.id ~= \"{config.Name}\"";

			fileWriter.WriteToFile(playsetListFile, injectedCodeAnd, 0x00000BB3, true, false);
			fileWriter.WriteToFile(playsetListFile, injectedCodeOr, 0x00001115, true, false);

			fileWriter.WriteToFile(playsetListFile, injectedCodeUI, 0x00002846, true, false);
			fileWriter.WriteToFile(playsetListFile, injectedCodeAdd, 0x000028FD, true, false);
		}

	}
}
