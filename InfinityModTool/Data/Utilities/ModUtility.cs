using InfinityModTool.Data;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using InfinityModTool.Data.Modifications;
using InfinityModTool.Extension;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using System.Data.Common;

namespace InfinityModTool.Utilities
{
	public class ModUtility
	{
		public class InstallInfo
		{
			public readonly InstallationStatus status;
			public readonly string[] conflicts;

			public InstallInfo(InstallationStatus status)
				: this(status, new string[0]) { }

			public InstallInfo(InstallationStatus status, string[] conflicts)
			{
				this.status = status;
				this.conflicts = conflicts;
			}
		}

		public enum InstallationStatus
		{
			Success,
			Conflict,
			Error
		}

		public static async Task<InstallInfo> ApplyChanges(Configuration configuration, GameModification[] mods)
		{
			var tempFolder = FileWriterUtility.CreateTempFolder();

			// Start with a blank canvas
			RemoveAllChanges(configuration);

			if (mods.Length == 0)
				return new InstallInfo(InstallationStatus.Success);

			try
			{
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
				var fileWrites = new Dictionary<string, List<FileWrite>>();

				FileWriterUtility.WriteToFile(virtualreaderDecompiledFile, characterJson, 0x0000A5C6, true, fileWrites, false);
				FileWriterUtility.WriteToFile(virtualreaderDecompiledFile, costumeCoinJson, 0x0001588F + 1, true, fileWrites, false);

				// Apply character specific actions
				foreach (var mod in characterMods)
				{
					if (ModRequiresReplacement(mod))
					{
						var offset = FindReplacementOffset(igpLocksFile, mod.Parameters["ReplacementCharacter"]);
						FileWriterUtility.WriteToFile(igpLocksFile, mod.GetConfig<CharacterModConfiguration>().PresentationData.Name, offset, false, fileWrites, true);
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

						FileWriterUtility.WriteToFileRange(virtualreaderDecompiledFile, GenerateCharacterJson(characterData), startIndex, endIndex, fileWrites, true);
					}
				}

				// Unlock playsets
				foreach (var mod in playsetMods)
				{
					var config = mod.GetConfig<PlaysetModConfiguration>();
					AddPlayset(config, playsetlistwin32DecompiledFile, fileWrites);
				}
			}
			catch (Exception ex)
			{
				Directory.Delete(tempFolder, true);
				RemoveAllChanges(configuration);

				return new InstallInfo(InstallationStatus.Error);
			}

			CopyFiles(configuration, tempFolder);
			Directory.Delete(tempFolder, true);

			return new InstallInfo(InstallationStatus.Success);
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

		private static void AddPlayset(PlaysetModConfiguration config, string playsetListFile, Dictionary<string, List<FileWrite>> fileWrites)
		{
			var injectedCodeUI = $"        self:AddListButton(\"{config.Name}\")\r\n";
			var injectedCodeAdd = $"    self:AddListButton(\"{config.Name}\")\r\n";
			var injectedCodeOr = $" or t.id ~= \"{config.Name}\"";
			var injectedCodeAnd = $" and t.id ~= \"{config.Name}\"";

			FileWriterUtility.WriteToFile(playsetListFile, injectedCodeAnd, 0x00000BB3, true, fileWrites, false);
			FileWriterUtility.WriteToFile(playsetListFile, injectedCodeOr, 0x00001115, true, fileWrites, false);

			FileWriterUtility.WriteToFile(playsetListFile, injectedCodeUI, 0x00002846, true, fileWrites, false);
			FileWriterUtility.WriteToFile(playsetListFile, injectedCodeAdd, 0x000028FD, true, fileWrites, false);
		}

	}
}
