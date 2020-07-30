using Microsoft.Extensions.Configuration;
using LitJson;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Controller;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using InfinityModTool.Data.InstallActions;
using InfinityModTool.Enums;

namespace InfinityModTool.Data.Utilities
{
	public class ModLoaderUtility
	{
#if DEBUG
		const string MOD_PATH = "..\\..\\..\\Mods";
		const string CHARACTER_ID_NAMES = "..\\..\\..\\character_ids.json";
#else
		const string MOD_PATH = "Mods";
		const string CHARACTER_ID_NAMES = "character_ids.json";
#endif

		private class ModPathInfo
		{
			public string executionPath;
			public string modPath;
			public string extractBasePath;
		}

		public class ModLoadResult
		{
			public readonly string modFileName;
			public readonly ModLoadStatus status;

			public ModLoadResult(string modPath, ModLoadStatus status)
			{
				this.modFileName = modPath;
				this.status = status;
			}
		}

		public static ListOption[] GetIDNameListOptions()
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var idNamePath = Path.Combine(executionPath, CHARACTER_ID_NAMES);

			var fileData = File.ReadAllText(idNamePath);
			var idNames = Newtonsoft.Json.JsonConvert.DeserializeObject<IDNames>(fileData);

			return idNames.CharacterIDs.Select(id => new ListOption(id.ID, id.DisplayName)).ToArray();
		}

		public static string GetModPath()
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			return Path.Combine(executionPath, MOD_PATH);
		}

		public static BaseModConfiguration[] LoadMods(double currentVersion, List<ModLoadResult> allResults)
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var modPath = Path.Combine(executionPath, MOD_PATH);
			var extractBasePath = Path.Combine(Global.APP_DATA_FOLDER, "Temp");

			var pathInfo = new ModPathInfo()
			{
				executionPath = executionPath,
				modPath = modPath,
				extractBasePath = extractBasePath
			};
			
			if (!Directory.Exists(modPath))
				Directory.CreateDirectory(modPath);

			DeleteAndRecreateFolder(extractBasePath);

			var mods = new List<BaseModConfiguration>();
			allResults.Clear();

			foreach (var file in Directory.GetFiles(modPath))
			{
				var fileInfo = new FileInfo(file);
				var result = TryLoadV1Mod(fileInfo, pathInfo, out var modData);

				if (result == ModLoadStatus.Success)
					mods.Add(modData);

				allResults.Add(new ModLoadResult(fileInfo.Name, result));
			}

			return mods.ToArray();
		}

		private static ModLoadStatus TryLoadV1Mod(FileInfo fileInfo, ModPathInfo pathInfo, out BaseModConfiguration modData)
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

					if (modData.Version > 2)
						return ModLoadStatus.UnsupportedVersion;

					modData = LoadModData(modData, extractFolder);
					modData.ModCachePath = extractFolder;

					// Temporary work around since we can't appear to load images from %APPDATA%
					var imageFileInfo = new FileInfo(Path.Join(modData.ModCachePath, modData.DisplayImage));
					var imageBytes = File.ReadAllBytes(imageFileInfo.FullName);
					modData.DisplayImageBase64 = $"data:image/{imageFileInfo.Extension};base64," + Convert.ToBase64String(imageBytes);

					var configValid = CheckModConfigurationIsValid(modData);

					return configValid ? ModLoadStatus.Success : ModLoadStatus.ConfigInvalid;
				}
				else
				{
					return ModLoadStatus.ExtensionInvalid;
				}
			}
			catch (Exception ex)
			{
				return ModLoadStatus.UnspecifiedFailure;
			}
			finally
			{
				if (!string.IsNullOrWhiteSpace(zipFile))
					File.Delete(zipFile);
			}
		}

		static bool CheckModConfigurationIsValid(BaseModConfiguration configuration)
		{
			bool result = true;

			// If we don't have any actions, no need to validate
			if (configuration.InstallActions == null)
				return true;

			foreach (var action in configuration.InstallActions)
			{
				if (action is FileMoveAction)
					result &= CheckFileMovAction(action as FileMoveAction);
				else if (action is FileCopyAction)
					result &= CheckFileCopyAction(action as FileCopyAction);
				else if (action is FileDeleteAction)
					result &= CheckFileDeleteAction(action as FileDeleteAction);
				else if (action is FileReplaceAction)
					result &= CheckFileReplaceAction(action as FileReplaceAction);
				else if (action is FileWriteAction)
					result &= CheckFileWriteAction(action as FileWriteAction);
				else if (action is QuickBMSExtractAction)
					result &= CheckQuickBMSExtractAction(action as QuickBMSExtractAction);
				else if (action is UnluacDecompileAction)
					result &= CheckUnluacDecompileAction(action as UnluacDecompileAction);
			}

			return result;
		}

		static bool CheckFileMovAction(FileMoveAction action)
		{
			return !string.IsNullOrWhiteSpace(action.TargetFile) && !string.IsNullOrWhiteSpace(action.DestinationPath);
		}

		static bool CheckFileCopyAction(FileCopyAction action)
		{
			return !string.IsNullOrWhiteSpace(action.TargetFile) && !string.IsNullOrWhiteSpace(action.DestinationPath);
		}

		static bool CheckFileDeleteAction(FileDeleteAction action)
		{
			return !string.IsNullOrWhiteSpace(action.TargetFile);
		}

		static bool CheckFileReplaceAction(FileReplaceAction action)
		{
			return !string.IsNullOrWhiteSpace(action.TargetFile) && !string.IsNullOrWhiteSpace(action.ReplacementFile);
		}

		static bool CheckFileWriteAction(FileWriteAction action)
		{
			if (string.IsNullOrWhiteSpace(action.TargetFile) || action.Content is null)
				return false;

			foreach (var content in action.Content)
			{
				if (!string.IsNullOrWhiteSpace(content.DataFilePath) && !string.IsNullOrWhiteSpace(content.Text))
					return false;

				if (content.Replace && !content.EndOffset.HasValue)
					return false;
			}

			return true;
		}

		static bool CheckQuickBMSExtractAction(QuickBMSExtractAction action)
		{
			return action.TargetFiles != null;
		}

		static bool CheckUnluacDecompileAction(UnluacDecompileAction action)
		{
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
	}
}
