﻿using InfinityModTool.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InfinityModTool.Data.Modifications;
using InfinityModTool.Extension;
using InfinityModTool.Data.InstallActions;
using System.Reflection;
using InfinityModTool.Enums;
using InfinityModTool.Models;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Drawing;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using InfinityModTool.Data.Models;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InfinityModTool.Utilities
{
	public class ModUtility
	{
#if DEBUG
		const string RESERVED_FILES = "..\\..\\..\\reserved_files.txt";
		const string CHARACTER_ID_NAMES = "..\\..\\..\\character_ids.json";
		const string AUTO_MAPPING_PATH = "..\\..\\..\\automapping.json";
#else
		const string RESERVED_FILES = "reserved_files.txt";
		const string CHARACTER_ID_NAMES = "character_ids.json";
		const string AUTO_MAPPING_PATH = "automapping.json";
#endif

		public class ReservedFileModificationException : Exception
		{
			public ReservedFileModificationException(string message) : base(message) { }
		}

		private Configuration configuration;
		QuickBMSAutoMappingCollection autoMappings;
		HashSet<string> reservedFiles;

		FileWriterUtility fileWriter;
		ModActionCollection modActions;

		string tempFolder;

		public List<ModCollision> conflicts = new List<ModCollision>();
		public readonly List<FileModification> modifications = new List<FileModification>();

		private List<string> decompiledFiles = new List<string>();
		private List<string> extractedFiles = new List<string>();
		private List<string> deletedFiles = new List<string>();
		private List<string> backedUpFiles = new List<string>();

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
			autoMappings = GetAutoMappings();
			fileWriter = new FileWriterUtility();

			// Start with a blank canvas
			RemoveAllChanges(configuration);

			if (mods.Length == 0)
				return InstallationStatus.Success;

			try
			{
				modActions = GetModActions(mods);

				// Write any data as required to the virtualreader file, make sure to offset by bytesWritten if needed

				foreach (var action in modActions.extractActions)
					await QuickBMSExtract(action, tempFolder, action.action.UseAutoMapping);

				foreach (var action in modActions.decompileActions)
					await UnluacDecompile(action, tempFolder);

				var actionStatus = PerformActions(modActions);

				if (actionStatus != InstallationStatus.Success)
					return actionStatus;
			}
			catch (Exception ex)
			{
				Logging.LogMessage(ex.ToString(), Logging.LogSeverity.Error);

				if (Directory.Exists(tempFolder))
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
			finally
			{
				if (Directory.Exists(tempFolder))
					Directory.Delete(tempFolder, true);
			}

			return InstallationStatus.Success;
		}

		private InstallationStatus PerformActions(ModActionCollection modActions)
		{
			foreach (var action in modActions.fileCopyActions)
			{
				var status = CopyFile(action);

				if (status != InstallationStatus.Success)
					return status;
			}

			foreach (var action in modActions.bulkFileCopyActions)
			{
				var status = BulkCopyFiles(action);

				if (status != InstallationStatus.Success)
					return status;
			}

			foreach (var action in modActions.fileReplaceActions)
			{
				var status = ReplaceFile(action);

				if (status != InstallationStatus.Success)
					return status;
			}

			foreach (var action in modActions.bulkFileReplaceActions)
			{
				var status = BulkReplaceFiles(action);

				if (status != InstallationStatus.Success)
					return status;
			}

			foreach (var action in modActions.fileWriteActions)
			{
				var status = WriteToFile(action);

				if (status != InstallationStatus.Success)
					return status;
			}

			foreach (var action in modActions.fileMoveActions)
			{
				var status = MoveFile(action);

				if (status != InstallationStatus.Success)
					return status;
			}

			foreach (var action in modActions.bulkFileMoveActions)
			{
				var status = BulkMoveFiles(action);

				if (status != InstallationStatus.Success)
					return status;
			}

			foreach (var action in modActions.fileDeleteActions)
			{
				var status = DeleteFile(action);

				if (status != InstallationStatus.Success)
					return status;
			}

			return InstallationStatus.Success;
		}

		public static ModActionCollection GetModActions(GameModification[] mods)
		{
			var collection = new ModActionCollection();

			// We need to collate these steps, so we can minimize collision issues
			foreach (var mod in mods)
				collection.AddActionsFromMod(mod);

			return collection;
		}

		private string CreateTempFolder()
		{
			var tempDirectory = Path.Combine(Global.APP_DATA_FOLDER, "ModInstallData");

			if (Directory.Exists(tempDirectory))
				Directory.Delete(tempDirectory, true);

			Directory.CreateDirectory(tempDirectory);

			return tempDirectory;
		}

		private async Task<InstallationStatus> QuickBMSExtract(ModAction<QuickBMSExtractAction> modAction, string tempFolder, bool autoUnpack)
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

				extractedFiles.Add(physicalTargetPath);

				foreach (var newFile in newFiles)
				{
					var targetPath = Path.Combine(fileInfo.Directory.FullName, newFile.Substring(newFolder.Length).TrimStart('\\').TrimStart('/'));
					MoveFile_Internal(newFile, targetPath, modAction.mod);
				}

				if (autoUnpack && HasAutoMapping(physicalTargetPath, configuration, out var autoMapping))
				{
					var autoUnpackActions = new ModActionCollection();
					autoUnpackActions.AddActions(modAction.mod, autoMapping.Actions);

					PerformActions(autoUnpackActions);
				}

				// Allow these files to be automatically deleted when finished with
				if (modAction.action.DeleteWhenComplete)
				{
					DeleteFile_Internal(physicalTargetPath, modAction.mod);
					deletedFiles.Add(physicalTargetPath);
				}
			}

			return InstallationStatus.Success;
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
				MoveFile_Internal(targetPath, physicalTargetPath, modAction.mod);

				decompiledFiles.Add(physicalTargetPath);
			}
		}

		private InstallationStatus MoveFile(ModAction<MoveFileAction> modAction)
		{
			var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod, configuration);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod, configuration);

			return MoveFile(physicalTargetPath, physicalDestinationPath, modAction.mod);
		}

		private InstallationStatus BulkMoveFiles(ModAction<MoveFilesAction> modAction)
		{
			var physicalDirectoryPath = ResolvePath(modAction.action.TargetDirectory, modAction.mod, configuration);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod, configuration);

			foreach (var file in GetFilteredFilesFromDirectory(physicalDirectoryPath, modAction.action.FileFilter, modAction.action.IncludeSubfolders))
			{
				var relativePath = file.Substring(physicalDirectoryPath.Length).TrimStart('\\').TrimStart('/');
				var status = MoveFile(file, Path.Combine(physicalDestinationPath, relativePath), modAction.mod);

				if (status != InstallationStatus.Success)
					return status;
			}

			return InstallationStatus.Success;
		}

		private InstallationStatus MoveFile(string targetFile, string destinationPath, GameModification mod)
		{
			// Have we already moved this file to the exact same destination?
			if (HasMovedFileToSameDestination(targetFile, destinationPath))
				return InstallationStatus.Success;

			if (ModCollisionTracker.HasMoveCollision(mod, targetFile, destinationPath, modifications, out var collision))
				return HandleCollision(collision);

			if (!File.Exists(targetFile))
				throw new Exception($"Unable to find target path: {targetFile}");

			MoveFile_Internal(targetFile, destinationPath, mod);
			return InstallationStatus.Success;
		}

		private bool HasMovedFileToSameDestination(string targetFile, string destinationPath)
		{
			var fileMoves = modifications.Where(m => m.FilePath == targetFile && m.Type == FileModificationType.Moved);

			if (fileMoves.Count() == 0)
				return false;

			return fileMoves.Select(m => m as MoveFileModification).LastOrDefault(m => m.DestinationPath == destinationPath) != null;
		}

		private InstallationStatus ReplaceFile(ModAction<ReplaceFileAction> modAction)
		{
			var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod, configuration);
			var physicalReplacementPath = ResolvePath(modAction.action.ReplacementFile, modAction.mod, configuration);

			return ReplaceFile(physicalReplacementPath, physicalTargetPath, modAction.mod);
		}

		private InstallationStatus BulkReplaceFiles(ModAction<ReplaceFilesAction> modAction)
		{
			var physicalDirectoryPath = ResolvePath(modAction.action.TargetDirectory, modAction.mod, configuration);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod, configuration);

			foreach (var file in GetFilteredFilesFromDirectory(physicalDirectoryPath, modAction.action.FileFilter, modAction.action.IncludeSubfolders))
			{
				var relativePath = file.Substring(physicalDirectoryPath.Length).TrimStart('\\').TrimStart('/');
				var status = ReplaceFile(file, Path.Combine(physicalDestinationPath, relativePath), modAction.mod);

				if (status != InstallationStatus.Success)
					return status;
			}

			return InstallationStatus.Success;
		}

		private InstallationStatus ReplaceFile(string replacementFile, string targetFile, GameModification mod)
		{
			if (ModCollisionTracker.HasReplaceCollision(mod, targetFile, replacementFile, modifications, out var collision))
				return HandleCollision(collision);
			
			if (!File.Exists(targetFile))
				throw new Exception($"Unable to find target path: {targetFile}");

			CopyFile_Internal(replacementFile, targetFile, mod);
			return InstallationStatus.Success;
		}

		private InstallationStatus CopyFile(ModAction<CopyFileAction> modAction)
		{
			var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod, configuration);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod, configuration);

			return CopyFile(physicalTargetPath, physicalDestinationPath, modAction.mod);
		}

		private InstallationStatus BulkCopyFiles(ModAction<CopyFilesAction> modAction)
		{
			var physicalDirectoryPath = ResolvePath(modAction.action.TargetDirectory, modAction.mod, configuration);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod, configuration);

			foreach (var file in GetFilteredFilesFromDirectory(physicalDirectoryPath, modAction.action.FileFilter, modAction.action.IncludeSubfolders))
			{
				var relativePath = file.Substring(physicalDirectoryPath.Length).TrimStart('\\').TrimStart('/');
				var status = CopyFile(file, Path.Combine(physicalDestinationPath, relativePath), modAction.mod);

				if (status != InstallationStatus.Success)
					return status;
			}

			return InstallationStatus.Success;
		}

		private InstallationStatus CopyFile(string targetFile, string destinationPath, GameModification mod)
		{
			if (ModCollisionTracker.HasCopyCollision(mod, targetFile, destinationPath, modifications, out var collision))
				return HandleCollision(collision);

			if (!File.Exists(targetFile))
				throw new Exception($"Unable to find target path: {targetFile}");

			// Copying files occurs near the start, and should not cause conflicts

			CopyFile_Internal(targetFile, destinationPath, mod);
			return InstallationStatus.Success;
		}

		private InstallationStatus DeleteFile(ModAction<DeleteFilesAction> modAction)
		{
			foreach (var file in modAction.action.TargetFiles)
			{
				var physicalTargetPath = ResolvePath(file, modAction.mod, configuration);

				// Check if this has already been deleted
				if (deletedFiles.Contains(physicalTargetPath) || !File.Exists(physicalTargetPath))
					continue;

				DeleteFile_Internal(physicalTargetPath, modAction.mod);

				deletedFiles.Add(physicalTargetPath);
			}

			// Delete actions do not currently cause collisions
			return InstallationStatus.Success;
		}

		private InstallationStatus WriteToFile(ModAction<WriteToFileAction> modAction)
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

				if (ModCollisionTracker.HasEditCollision(modAction.mod, physicalTargetPath, content, fileWriter, modifications, out var collision))
					return HandleCollision(collision);

				if (IsReservedFile(physicalTargetPath))
				{
					modifications.Add(new EditFileModification(physicalTargetPath, true, modAction.mod.Config.ModID));
					BackupFile(physicalTargetPath);
				}
				else
				{
					modifications.Add(new EditFileModification(physicalTargetPath, false, modAction.mod.Config.ModID));
				}

				if (content.EndOffset.HasValue)
					fileWriter.WriteToFileRange(physicalTargetPath, dataToWrite, content.StartOffset, content.EndOffset.Value, false);
				else
					fileWriter.WriteToFile(physicalTargetPath, dataToWrite, content.StartOffset, !content.Replace, false);
			}

			return InstallationStatus.Success;
		}

		private void MoveFile_Internal(string targetPath, string destinationPath, GameModification mod)
		{
			if (IsReservedFile(targetPath))
			{
				modifications.Add(new MoveFileModification(targetPath, destinationPath, true, mod.Config.ModID));
				BackupFile(targetPath);
			}
			else
			{
				modifications.Add(new MoveFileModification(targetPath, destinationPath, false, mod.Config.ModID));
			}

			if (IsReservedFile(destinationPath))
			{
				modifications.Add(new ReplaceFileModification(destinationPath, true, mod.Config.ModID));
				BackupFile(destinationPath);
			}
			else
			{
				modifications.Add(new AddFileModification(destinationPath, false, mod.Config.ModID));
			}

			Directory.CreateDirectory(new FileInfo(destinationPath).DirectoryName);
			File.Move(targetPath, destinationPath, true);
		}

		private void CopyFile_Internal(string targetPath, string destinationPath, GameModification mod)
		{
			if (IsReservedFile(destinationPath))
			{
				modifications.Add(new ReplaceFileModification(destinationPath, true, mod.Config.ModID));
				BackupFile(destinationPath);
			}
			else
			{
				modifications.Add(new AddFileModification(destinationPath, false, mod.Config.ModID));
			}

			Directory.CreateDirectory(new FileInfo(destinationPath).DirectoryName);
			File.Copy(targetPath, destinationPath, true);
		}

		private void DeleteFile_Internal(string targetPath, GameModification mod)
		{
			if (IsReservedFile(targetPath))
			{
				modifications.Add(new DeleteFileModification(targetPath, true, mod.Config.ModID));
				BackupFile(targetPath);
			}
			else
			{
				modifications.Add(new DeleteFileModification(targetPath, false, mod.Config.ModID));
			}

			File.Delete(targetPath);
		}

		private IEnumerable<string> GetFilteredFilesFromDirectory(string folder, string pattern, bool includeSubFolders)
		{
			var files = Directory.GetFiles(folder, "*", includeSubFolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
			return files.Where(f => Regex.IsMatch(new FileInfo(f).Name, pattern));
		}

		private string GetRelativePath(string path)
		{
			return path.Substring(configuration.SteamInstallationPath.Length);
		}

		private void BackupFile(string path)
		{
			string relativePath = path.Substring(configuration.SteamInstallationPath.Length).Trim('\\').Trim('.');
			string backupPath = Path.Combine(Global.APP_DATA_FOLDER, "Backup", relativePath);

			// Prevent issues from occuring if we modify an already backed-up file
			if (File.Exists(backupPath))
				return;

			Directory.CreateDirectory(new FileInfo(backupPath).DirectoryName);
			File.Copy(path, backupPath, true);

			backedUpFiles.Add(relativePath);
		}

		private HashSet<string> GetReservedFiles()
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var filePath = Path.Combine(executionPath, RESERVED_FILES);

			Logging.LogMessage($"Loading reserved file list from: {filePath}", Logging.LogSeverity.Info);

			string[] reservedFiles = File.ReadAllLines(filePath);
			var hashSet = new HashSet<string>();

			foreach (var file in reservedFiles)
				hashSet.Add(Path.Combine(configuration.SteamInstallationPath, file));

			return hashSet;
		}

		public static ListOption[] GetIDNameListOptions()
		{
			// Temporarily removed...
			return null;
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
				return ResolveGamePath(path, config);

			if (path.StartsWith("[MOD]", StringComparison.InvariantCultureIgnoreCase))
				return ResolveModPath(path, mod);

			throw new Exception("Supplied path must begin with the following: [GAME], [MOD]");
		}

		public static string ResolveGamePath(string path, Configuration config)
		{
			return Path.Combine(config.SteamInstallationPath, path.Substring(6, path.Length - 6).TrimStart('\\').TrimStart('/'));
		}

		public static string ResolveModPath(string path, GameModification mod)
		{
			return Path.Combine(mod.Config.ModCachePath, path.Substring(5, path.Length - 5).TrimStart('\\').TrimStart('/'));
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

		private InstallationStatus HandleCollision(ModCollision collision)
		{
			conflicts.Add(collision);
			return collision.severity == ModCollisionSeverity.Clash ? InstallationStatus.UnresolvableConflict : InstallationStatus.ResolvableConflict;
		}

		private QuickBMSAutoMappingCollection GetAutoMappings()
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var filePath = Path.Combine(executionPath, AUTO_MAPPING_PATH);
			var json = File.ReadAllText(filePath);

			return JsonConvert.DeserializeObject<QuickBMSAutoMappingCollection>(json, new ModInstallActionConverter());
		}

		private bool HasAutoMapping(string resourceFile, Configuration config, out QuickBMSAutoMapping autoMapping)
		{
			foreach (var map in autoMappings.QuickBMSAutoMappings)
			{
				var files = Directory.GetFiles(ModUtility.ResolveGamePath(map.TargetDirectory, config));
				var filteredFiles = files.Where(f => Regex.IsMatch(f, map.FileFilter));

				if (filteredFiles.Any(f => f.Equals(resourceFile, StringComparison.InvariantCultureIgnoreCase)))
				{
					autoMapping = map;
					return true;
				}
			}

			autoMapping = null;
			return false;
		}
	}
}
