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

namespace InfinityModTool.Utilities
{
	public class ModUtility
	{
		public static async Task ApplyChanges(Configuration configuration, CharacterModification[] data)
		{
			var tempFolder = FileWriterUtility.CreateTempFolder();

			// Start with a blank canvas
			RemoveAllChanges(configuration);

			if (data.Length == 0)
				return;

			try
			{
				await ExtractFiles(configuration, tempFolder);
				var json = GenerateCharacterJson(data);
				
				var virtualreaderFile = Path.Combine(tempFolder, "presentation", "virtualreaderpc_data.lua");
				var virtualreaderDecompiledFile = Path.Combine(tempFolder, "virtualreaderpc_data_decomp.lua");
				var igpLocksFile = Path.Combine(tempFolder, "igp", "locks__lua.chd");

				await UnluacUtility.Decompile(virtualreaderFile, virtualreaderDecompiledFile);
				
				// Write any data as required to the virtualreader file, make sure to offset by bytesWritten if needed
				int bytesWritten = 0;
				FileWriterUtility.WriteToFile(virtualreaderDecompiledFile, json, 0x0000A5C6, true, out bytesWritten);

				foreach (var character in data.Where(c => !string.IsNullOrEmpty(c.ReplacementCharacter)))
				{
					var offset = FindReplacementOffset(igpLocksFile, character.ReplacementCharacter);
					FileWriterUtility.WriteToFile(igpLocksFile, character.Data.Name, offset, false, out bytesWritten);
				}
			}
			catch (Exception ex)
			{
				Directory.Delete(tempFolder, true);
				return;
			}

			CopyFiles(configuration, tempFolder);
			Directory.Delete(tempFolder, true);
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

		private static string GenerateCharacterJson(CharacterModification[] data)
		{
			var presentationDataToWrite = data.Where(d => d.Data.WriteToCharacterList);
			var jsonWriter = new StringBuilder();

			foreach (var character in presentationDataToWrite)
			{
				var characterJson = GenerateCharacterJson(character.Data);
				jsonWriter.Append(characterJson);
			}

			return jsonWriter.ToString();
		}

		private static string GenerateCharacterJson(CharacterData data)
		{
			var sb = new StringBuilder();

			sb.Append(",\r\n  {");
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

		private static int FindReplacementOffset(string lockFile, string replacementName)
		{
			var fileBytes = File.ReadAllBytes(lockFile);
			var replacementNameBytes = Encoding.UTF8.GetBytes(replacementName);

			int subSearchLength = replacementNameBytes.Length;
			bool match = false;
			int i, j;

			for (i = 0; i < fileBytes.Length; i++)
			{
				match = true;

				for (j = 0; j < subSearchLength; j++)
				{
					if (fileBytes[i + j] != replacementNameBytes[j])
					{
						match = false;
						break;
					}
				}

				if (match)
					return i;
			}

			throw new System.Exception($"Unable to find the string '{replacementName}' within the requested file");
		}

		private static void CopyFiles(Configuration configuration, string tempFolder)
		{
			var presentationFolder = Path.Combine(configuration.SteamInstallationPath, "assets", "presentation");
			var igpFolder = Path.Combine(configuration.SteamInstallationPath, "assets", "gamedb", "igp");
			var flashFolder = Path.Combine(configuration.SteamInstallationPath, "assets", "flash");
			var virtualReaderFile = Path.Combine(presentationFolder, "virtualreaderpc_data.lua");

			var tempPresentationFolder = Path.Combine(tempFolder, "presentation");
			var tempIgpFolder = Path.Combine(tempFolder, "igp");
			var tempVirtualReaderFile = Path.Combine(tempFolder, "virtualreaderpc_data_decomp.lua");
			var tempFlashFile = Path.Combine(tempFolder, "presentation", "flash", "container.gfx");

			// Copy all items from the presentation folder, except for the flash folder
			foreach (var file in Directory.GetFiles(tempPresentationFolder))
				CopyFileIntoFolder(file, presentationFolder, true);

			// Override the virtualreaderpc_data.lua file
			File.Copy(tempVirtualReaderFile, virtualReaderFile, true);

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
	}
}
