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
using UnluacNET;
using System.Net.NetworkInformation;
using System.Runtime;
using ElectronNET.API.Entities;
using System.Security.Cryptography;

namespace InfinityModTool.Utilities
{
	public class ModUtility
	{
#if DEBUG
		const string RESERVED_FILES = "..\\..\\..\\reserved_files.txt";
#else
		const string RESERVED_FILES = "reserved_files.txt";
#endif

		public class ReservedFileModificationException : Exception
		{
			public ReservedFileModificationException(string message) : base(message) { }
		}

		private Configuration configuration;
		HashSet<string> reservedFiles;
		string tempFolder;

		public ModCollision[] conflicts = new ModCollision[0];
		public readonly List<FileModification> modifications = new List<FileModification>();

		private List<string> decompiledFiles = new List<string>();
		private List<string> extractedFiles = new List<string>();

		public ModUtility(Configuration configuration)
		{
			this.configuration = configuration;
		}

		public async Task<InstallationStatus> ApplyChanges(GameModification[] mods, bool ignoreWarnings)
		{
			return await ApplyChangesInternal(mods, ignoreWarnings, false);
		}
		
		private async Task<InstallationStatus> ApplyChangesInternal(GameModification[] mods, bool ignoreWarnings, bool reverting)
		{
			tempFolder = CreateTempFolder();
			reservedFiles = GetReservedFiles();

			// Start with a blank canvas
			RemoveAllChanges(configuration);

			if (mods.Length == 0)
				return InstallationStatus.Success;

			try
			{
				var modActions = GetModActions(mods);
				var collisionTracker = new ModCollisionTracker(configuration);

				conflicts = collisionTracker.CheckForPotentialModCollisions(mods.Last(), modActions);

				if (conflicts.Length > 0)
					return conflicts.Any(c => c.severity == ModCollisionSeverity.Clash) ? InstallationStatus.UnresolvableConflict : InstallationStatus.ResolvableConflict;

				// Write any data as required to the virtualreader file, make sure to offset by bytesWritten if needed
				var fileWriter = new FileWriterUtility();

				foreach (var extractAction in modActions.extractActions)
					await QuickBMSExtract(extractAction, tempFolder);

				foreach (var decompileAction in modActions.decompileActions)
					await UnluacDecompile(decompileAction, tempFolder);

				foreach (var fileCopyAction in modActions.fileCopyActions)
					CopyFile(fileCopyAction);

				foreach (var fileWriteAction in modActions.fileWriteActions)
					WriteToFile(fileWriteAction, fileWriter);

				foreach (var fileReplaceAction in modActions.fileReplaceActions)
					ReplaceFile(fileReplaceAction);

				foreach (var fileMoveAction in modActions.fileMoveActions)
					MoveFile(fileMoveAction);

				foreach (var fileDeleteAction in modActions.fileDeleteActions)
					DeleteFile(fileDeleteAction);
			}
			catch (Exception ex)
			{
				Logging.LogMessage(ex.ToString(), Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

				Directory.Delete(tempFolder, true);

				if (reverting)
				{
					// If we error out during revert, delete everything (something has gone badly wrong)
					RemoveAllChanges(configuration);
					return InstallationStatus.FatalError;
				}
				else
				{
					// Revert back to the previous install state
					await ApplyChangesInternal(mods.Take(mods.Count() - 1).ToArray(), true, true);
					return InstallationStatus.RolledBackError;
				}
			}

			Directory.Delete(tempFolder, true);

			return InstallationStatus.Success;
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
				fileDeleteActions.AddRange(installActions.Where(a => a.Action.SafeEquals("DeleteFiles", ignoreCase: true)).Select(a => new ModAction<FileDeleteAction>(mod, a as FileDeleteAction)));
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

		private string CreateTempFolder()
		{
			var tempDirectory = Path.Combine(Global.APP_DATA_FOLDER, "TempModInstallData");

			if (Directory.Exists(tempDirectory))
				Directory.Delete(tempDirectory, true);

			Directory.CreateDirectory(tempDirectory);

			return tempDirectory;
		}

		private async Task QuickBMSExtract(ModAction<QuickBMSExtractAction> modAction, string tempFolder)
		{
			foreach (var file in modAction.action.TargetFiles)
			{
				var physicalTargetPath = ResolvePath(file, modAction.mod, configuration);
				var fileInfo = new FileInfo(physicalTargetPath);

				// Check if this has already been extracted
				if (extractedFiles.Contains(physicalTargetPath))
					continue;

				if (!File.Exists(physicalTargetPath))
					throw new Exception($"Unable to find target path: {physicalTargetPath}");

				await QuickBMSUtility.ExtractFiles(physicalTargetPath, tempFolder);

				// Move all extracted files into the root folder
				var newFolder = Path.Combine(tempFolder, Path.GetFileNameWithoutExtension(fileInfo.Name));
				var newFiles = Directory.GetFiles(newFolder, "*", SearchOption.AllDirectories);

				foreach (var newFile in newFiles)
				{
					var targetPath = Path.Combine(fileInfo.Directory.FullName, newFile.Substring(newFolder.Length).TrimStart('\\').TrimStart('/'));
					MoveFile_Internal(newFile, targetPath);
				}

				extractedFiles.Add(physicalTargetPath);
			}
		}

		private async Task UnluacDecompile(ModAction<UnluacDecompileAction> modAction, string tempFolder)
		{
			foreach (var file in modAction.action.TargetFiles)
			{
				var physicalTargetPath = ResolvePath(file, modAction.mod, configuration);
				var fileInfo = new FileInfo(physicalTargetPath);

				var targetPath = Path.Combine(tempFolder, $"{Path.GetFileNameWithoutExtension(fileInfo.Name)}_decomp{fileInfo.Extension}");

				// Check if this has already been decompiled
				if (decompiledFiles.Contains(physicalTargetPath))
					continue;

				if (!File.Exists(physicalTargetPath))
					throw new Exception($"Unable to find target path: {physicalTargetPath}");

				// Decompile the file, and replace the file
				await UnluacUtility.Decompile(physicalTargetPath, targetPath);
				MoveFile_Internal(targetPath, physicalTargetPath);

				decompiledFiles.Add(physicalTargetPath);
			}
		}

		private void MoveFile(ModAction<FileMoveAction> modAction)
		{
			var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod, configuration);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod, configuration);

			if (!File.Exists(physicalTargetPath))
				throw new Exception($"Unable to find target path: {physicalTargetPath}");

			MoveFile_Internal(physicalTargetPath, physicalDestinationPath);
		}

		private void ReplaceFile(ModAction<FileReplaceAction> modAction)
		{
			var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod, configuration);
			var physicalDestinationPath = ResolvePath(modAction.action.ReplacementFile, modAction.mod, configuration);

			if (!File.Exists(physicalTargetPath))
				throw new Exception($"Unable to find target path: {physicalTargetPath}");

			MoveFile_Internal(physicalTargetPath, physicalDestinationPath);
		}

		private void CopyFile(ModAction<FileCopyAction> modAction)
		{
			var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod, configuration);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod, configuration);

			if (!File.Exists(physicalTargetPath))
				throw new Exception($"Unable to find target path: {physicalTargetPath}");

			CopyFile_Internal(physicalTargetPath, physicalDestinationPath);
		}

		private void DeleteFile(ModAction<FileDeleteAction> modAction)
		{
			foreach (var file in modAction.action.TargetFiles)
			{
				var physicalTargetPath = ResolvePath(file, modAction.mod, configuration);

				if (!File.Exists(physicalTargetPath))
					throw new Exception($"Unable to find target path: {physicalTargetPath}");

				DeleteFile_Internal(physicalTargetPath);
			}
		}

		private void WriteToFile(ModAction<FileWriteAction> modAction, FileWriterUtility fileWriter)
		{
			foreach (var content in modAction.action.Content)
			{
				var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod, configuration);
				string filePath = null;

				if (!File.Exists(physicalTargetPath))
					throw new Exception($"Unable to find target path: {physicalTargetPath}");

				if (!string.IsNullOrEmpty(content.DataFilePath))
					filePath = ResolvePath(content.DataFilePath, modAction.mod, configuration);

				var dataToWrite = content.Text ?? File.ReadAllText(filePath);

				if (IsReservedFile(physicalTargetPath))
				{
					modifications.Add(new FileModification(GetRelativePath(physicalTargetPath), FileModificationType.Edited, true));
					BackupFile(physicalTargetPath);
				}
				else
				{
					modifications.Add(new FileModification(GetRelativePath(physicalTargetPath), FileModificationType.Edited, false));
				}

				if (content.EndOffset.HasValue)
					fileWriter.WriteToFileRange(physicalTargetPath, dataToWrite, content.StartOffset, content.EndOffset.Value, false);
				else
					fileWriter.WriteToFile(physicalTargetPath, dataToWrite, content.StartOffset, !content.Replace, false);
			}
		}

		private void MoveFile_Internal(string targetPath, string destinationPath)
		{
			if (IsReservedFile(targetPath))
			{
				modifications.Add(new FileModification(GetRelativePath(targetPath), FileModificationType.Moved, true));
				BackupFile(targetPath);
			}
			else
			{
				modifications.Add(new FileModification(GetRelativePath(targetPath), FileModificationType.Moved, false));
			}

			if (IsReservedFile(destinationPath))
			{
				modifications.Add(new FileModification(GetRelativePath(destinationPath), FileModificationType.Replaced, true));
				BackupFile(destinationPath);
			}
			else
			{
				modifications.Add(new FileModification(GetRelativePath(destinationPath), FileModificationType.Added, false));
			}

			Directory.CreateDirectory(new FileInfo(destinationPath).DirectoryName);
			File.Move(targetPath, destinationPath, true);
		}

		private void CopyFile_Internal(string targetPath, string destinationPath)
		{
			if (IsReservedFile(destinationPath))
			{
				modifications.Add(new FileModification(GetRelativePath(destinationPath), FileModificationType.Replaced, true));
				BackupFile(destinationPath);
			}
			else
			{
				modifications.Add(new FileModification(GetRelativePath(destinationPath), FileModificationType.Added, false));
			}

			Directory.CreateDirectory(new FileInfo(destinationPath).DirectoryName);
			File.Copy(targetPath, destinationPath, true);
		}

		private void DeleteFile_Internal(string targetPath)
		{
			if (IsReservedFile(targetPath))
			{
				modifications.Add(new FileModification(GetRelativePath(targetPath), FileModificationType.Deleted, true));
				BackupFile(targetPath);
			}
			else
			{
				modifications.Add(new FileModification(GetRelativePath(targetPath), FileModificationType.Deleted, false));
			}

			File.Delete(targetPath);
		}

		private string GetRelativePath(string path)
		{
			return path.Substring(configuration.SteamInstallationPath.Length);
		}

		private void BackupFile(string path)
		{
			string relativePath = path.Substring(configuration.SteamInstallationPath.Length).Trim('\\').Trim('.');
			string backupPath = Path.Combine(Global.APP_DATA_FOLDER, "Backup", relativePath);

			Directory.CreateDirectory(new FileInfo(backupPath).DirectoryName);
			File.Copy(path, backupPath, true);
		}

		private string GetBackupFilePath(string path)
		{
			string relativePath = path.Substring(configuration.SteamInstallationPath.Length).Trim('\\').Trim('.');
			string backupPath = Path.Combine(Global.APP_DATA_FOLDER, "Backup", relativePath);

			return backupPath;
		}

		private HashSet<string> GetReservedFiles()
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var filePath = Path.Combine(executionPath, RESERVED_FILES);

			string[] reservedFiles = File.ReadAllLines(filePath);
			var hashSet = new HashSet<string>();

			foreach (var file in reservedFiles)
				hashSet.Add(Path.Combine(configuration.SteamInstallationPath, file));

			return hashSet;
		}

		private bool IsReservedFile(string path)
		{
			return reservedFiles.Contains(path);
		}

		public static string ResolvePath(string path, GameModification mod, Configuration config)
		{
			if (string.IsNullOrEmpty(path))
				return null;

			if (path.StartsWith("[GAME]", StringComparison.InvariantCultureIgnoreCase))
				return Path.Combine(config.SteamInstallationPath, path.Substring(6, path.Length - 6).TrimStart('\\').TrimStart('/'));

			if (path.StartsWith("[MOD]", StringComparison.InvariantCultureIgnoreCase))
				return Path.Combine(mod.Config.ModCachePath, path.Substring(5, path.Length - 5).TrimStart('\\').TrimStart('/'));

			throw new Exception("Supplied path must begin with the following: [GAME], [MOD]");
		}

		public void RemoveAllChanges(Configuration configuration)
		{
			var backupPath = Path.Combine(Global.APP_DATA_FOLDER, "Backup");
			Directory.CreateDirectory(backupPath);

			var allGameFiles = Directory.GetFiles(Path.Combine(configuration.SteamInstallationPath, "assets"), "*", SearchOption.AllDirectories);
			var allBackupFiles = Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories);
			
			foreach (var file in allGameFiles)
			{
				if (!IsReservedFile(file))
					File.Delete(file);
			}

			foreach (var file in allBackupFiles)
			{
				var relativePath = file.Substring(backupPath.Length).Trim('\\').Trim('/');
				var gamePath = Path.Combine(configuration.SteamInstallationPath, relativePath);

				File.Copy(file, gamePath, true);
			}

			// Remove all modifications
			modifications.Clear();

			foreach (var file in allBackupFiles)
				File.Delete(file);
		}

	}
}
