using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InfinityModEngine.InstallActions;
using InfinityModEngine.Enums;
using InfinityModEngine.Models;
using InfinityModEngine.Common.Logging;
using System.Text.RegularExpressions;
using InfinityModEngine.Utilities;

namespace InfinityModEngine
{
	public class ModInstaller
	{
		public class ReservedFileModificationException : Exception
		{
			public ReservedFileModificationException(string message) : base(message) { }
		}

		Configuration configuration;
		GameIntegration integration;
		ILogger logger;

		FileWriter fileWriter;
		ModActionCollection modActions;

		private List<ModCollision> conflicts;
		private List<FileModification> modifications;

		HashSet<string> reservedFiles;
		private List<string> decompiledFiles = new List<string>();
		private List<string> extractedFiles = new List<string>();
		private List<string> deletedFiles = new List<string>();
		private List<string> backedUpFiles = new List<string>();

		public ModInstaller(Configuration configuration, GameIntegration integration, ILogger logger = null)
		{
			this.configuration = configuration;
			this.integration = integration;
			this.logger = logger ?? new ConsoleLogger();
		}

		public async Task<ModInstallResult> ApplyChanges(ModInstallationInfo[] mods, bool ignoreWarnings)
		{
			modifications = new List<FileModification>(configuration.Modifications);
			return await ApplyChangesInternal(mods, ignoreWarnings, false);
		}

		private async Task<ModInstallResult> ApplyChangesInternal(ModInstallationInfo[] mods, bool ignoreWarnings, bool reverting)
		{
			// Start with a blank canvas
			RemoveAllChanges();

			conflicts = new List<ModCollision>();
			modifications = new List<FileModification>();
			decompiledFiles = new List<string>();
			extractedFiles = new List<string>();
			deletedFiles = new List<string>();
			backedUpFiles = new List<string>();

			Directory.CreateDirectory(configuration.TempFolder);
			Directory.CreateDirectory(configuration.BackupFolder);

			// Our game state has been restored (if nothing has been externally modified), so track changes
			reservedFiles = new HashSet<string>(Directory.GetFiles(configuration.TargetPath, "*", SearchOption.AllDirectories));

			fileWriter = new FileWriter();

			if (mods.Length == 0)
				return new ModInstallResult(InstallationStatus.Success, conflicts, modifications);

			try
			{
				modActions = GetModActions(mods);

				// Write any data as required to the virtualreader file, make sure to offset by bytesWritten if needed

				foreach (var action in modActions.extractActions)
					await QuickBMSExtract(action, configuration.TempFolder, action.action.UseAutoMapping);

				foreach (var action in modActions.decompileActions)
					await UnluacDecompile(action, configuration.TempFolder);

				var actionStatus = PerformActions(modActions);

				if (actionStatus != InstallationStatus.Success)
				{
					if (actionStatus == InstallationStatus.ResolvableConflict && ignoreWarnings)
						return new ModInstallResult(InstallationStatus.Success, conflicts, modifications);
					else
						return new ModInstallResult(actionStatus, conflicts, modifications);
				}
			}
			catch (Exception ex)
			{
				logger.Log(ex.ToString(), LogSeverity.Error);

				if (Directory.Exists(configuration.TempFolder))
					Directory.Delete(configuration.TempFolder, true);

				if (reverting)
				{
					// If we error out during revert, delete everything (something has gone badly wrong)
					RemoveAllChanges();
					return new ModInstallResult(InstallationStatus.FatalError, conflicts, modifications);
				}
				else
				{
					// Revert back to the previous install state
					await ApplyChangesInternal(mods.Take(mods.Count() - 1).ToArray(), true, true);
					return new ModInstallResult(InstallationStatus.RolledBackError, conflicts, modifications);
				}
			}
			finally
			{
				if (Directory.Exists(configuration.TempFolder))
					Directory.Delete(configuration.TempFolder, true);
			}

			return new ModInstallResult(InstallationStatus.Success, conflicts, modifications);
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

		public static ModActionCollection GetModActions(ModInstallationInfo[] mods)
		{
			var collection = new ModActionCollection();

			// We need to collate these steps, so we can minimize collision issues
			foreach (var mod in mods)
				collection.AddActionsFromMod(mod);

			return collection;
		}

		private async Task<InstallationStatus> QuickBMSExtract(ModAction<QuickBMSExtractAction> modAction, string tempFolder, bool autoUnpack)
		{
			foreach (var file in modAction.action.TargetFiles)
			{
				var physicalTargetPath = ResolvePath(file, modAction.mod);
				var fileInfo = new FileInfo(physicalTargetPath);

				// Check if this has already been extracted
				if (extractedFiles.Contains(physicalTargetPath))
					continue;

				if (!File.Exists(physicalTargetPath))
					throw new Exception($"Unable to find target path: {physicalTargetPath}");

				await QuickBMSUtility.ExtractFiles(physicalTargetPath, tempFolder, configuration.ToolPath);

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
				var physicalTargetPath = ResolvePath(file, modAction.mod);
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
			var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod);

			return MoveFile(physicalTargetPath, physicalDestinationPath, modAction.mod);
		}

		private InstallationStatus BulkMoveFiles(ModAction<MoveFilesAction> modAction)
		{
			var physicalDirectoryPath = ResolvePath(modAction.action.TargetDirectory, modAction.mod);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod);

			foreach (var file in GetFilteredFilesFromDirectory(physicalDirectoryPath, modAction.action.FileFilter, modAction.action.IncludeSubfolders))
			{
				var relativePath = file.Substring(physicalDirectoryPath.Length).TrimStart('\\').TrimStart('/');
				var status = MoveFile(file, Path.Combine(physicalDestinationPath, relativePath), modAction.mod);

				if (status != InstallationStatus.Success)
					return status;
			}

			return InstallationStatus.Success;
		}

		private InstallationStatus MoveFile(string targetFile, string destinationPath, ModInstallationInfo mod)
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
			var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod);
			var physicalReplacementPath = ResolvePath(modAction.action.ReplacementFile, modAction.mod);

			return ReplaceFile(physicalReplacementPath, physicalTargetPath, modAction.mod);
		}

		private InstallationStatus BulkReplaceFiles(ModAction<ReplaceFilesAction> modAction)
		{
			var physicalDirectoryPath = ResolvePath(modAction.action.TargetDirectory, modAction.mod);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod);

			foreach (var file in GetFilteredFilesFromDirectory(physicalDirectoryPath, modAction.action.FileFilter, modAction.action.IncludeSubfolders))
			{
				var relativePath = file.Substring(physicalDirectoryPath.Length).TrimStart('\\').TrimStart('/');
				var status = ReplaceFile(file, Path.Combine(physicalDestinationPath, relativePath), modAction.mod);

				if (status != InstallationStatus.Success)
					return status;
			}

			return InstallationStatus.Success;
		}

		private InstallationStatus ReplaceFile(string replacementFile, string targetFile, ModInstallationInfo mod)
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
			var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod);

			return CopyFile(physicalTargetPath, physicalDestinationPath, modAction.mod);
		}

		private InstallationStatus BulkCopyFiles(ModAction<CopyFilesAction> modAction)
		{
			var physicalDirectoryPath = ResolvePath(modAction.action.TargetDirectory, modAction.mod);
			var physicalDestinationPath = ResolvePath(modAction.action.DestinationPath, modAction.mod);

			foreach (var file in GetFilteredFilesFromDirectory(physicalDirectoryPath, modAction.action.FileFilter, modAction.action.IncludeSubfolders))
			{
				var relativePath = file.Substring(physicalDirectoryPath.Length).TrimStart('\\').TrimStart('/');
				var status = CopyFile(file, Path.Combine(physicalDestinationPath, relativePath), modAction.mod);

				if (status != InstallationStatus.Success)
					return status;
			}

			return InstallationStatus.Success;
		}

		private InstallationStatus CopyFile(string targetFile, string destinationPath, ModInstallationInfo mod)
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
				var physicalTargetPath = ResolvePath(file, modAction.mod);

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
				var physicalTargetPath = ResolvePath(modAction.action.TargetFile, modAction.mod);
				string filePath = null;

				if (!File.Exists(physicalTargetPath))
					throw new Exception($"Unable to find target path: {physicalTargetPath}");

				if (!string.IsNullOrEmpty(content.DataFilePath))
					filePath = ResolvePath(content.DataFilePath, modAction.mod);

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

		private void MoveFile_Internal(string targetPath, string destinationPath, ModInstallationInfo mod)
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

		private void CopyFile_Internal(string targetPath, string destinationPath, ModInstallationInfo mod)
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

		private void DeleteFile_Internal(string targetPath, ModInstallationInfo mod)
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
			return path.Substring(configuration.TargetPath.Length);
		}

		private void BackupFile(string path)
		{
			string relativePath = path.Substring(configuration.TargetPath.Length).Trim('\\').Trim('.');
			string backupPath = Path.Combine(configuration.BackupFolder, relativePath);

			// Prevent issues from occuring if we modify an already backed-up file
			if (File.Exists(backupPath))
				return;

			Directory.CreateDirectory(new FileInfo(backupPath).DirectoryName);
			File.Copy(path, backupPath, true);

			backedUpFiles.Add(relativePath);
		}

		private bool IsReservedFile(string path)
		{
			return reservedFiles.Contains(path);
		}

		private string ResolvePath(string path, ModInstallationInfo mod)
		{
			if (string.IsNullOrEmpty(path))
				return null;

			if (path.StartsWith("[GAME]", StringComparison.InvariantCultureIgnoreCase))
				return ResolveGamePath(path);

			if (path.StartsWith("[MOD]", StringComparison.InvariantCultureIgnoreCase))
				return ResolveModPath(path, mod);

			throw new Exception("Supplied path must begin with the following: [GAME], [MOD]");
		}

		private string ResolveGamePath(string path)
		{
			return Path.Combine(configuration.TargetPath, path.Substring(6, path.Length - 6).TrimStart('\\').TrimStart('/'));
		}

		private string ResolveModPath(string path, ModInstallationInfo mod)
		{
			return Path.Combine(configuration.CacheFolder, mod.Config.CacheFolderName, path.Substring(5, path.Length - 5).TrimStart('\\').TrimStart('/'));
		}

		private void RemoveAllChanges()
		{
			var backupPath = configuration.BackupFolder;
			var backupFiles = Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories);
			
			// Remove all files that were added as a part of the install process
			foreach (var modification in modifications.Where(m => m.Type == FileModificationType.Added))
			{
				var filePath = Path.Combine(configuration.TargetPath, modification.FilePath);

				if (File.Exists(filePath))
					File.Delete(filePath);
			}

			// Replace all files with those stored in the backup location
			foreach (var file in backupFiles)
			{
				var relativePath = file.Substring(backupPath.Length).Trim('\\').Trim('/');
				var gamePath = Path.Combine(configuration.TargetPath, relativePath);

				File.Copy(file, gamePath, true);
			}

			// Remove all modifications
			modifications.Clear();

			foreach (var file in backupFiles)
				File.Delete(file);
		}

		private InstallationStatus HandleCollision(ModCollision collision)
		{
			conflicts.Add(collision);
			return collision.severity == ModCollisionSeverity.Clash ? InstallationStatus.UnresolvableConflict : InstallationStatus.ResolvableConflict;
		}

		private bool HasAutoMapping(string resourceFile, Configuration config, out QuickBMSAutoMapping autoMapping)
		{
			foreach (var map in integration.QuickBMSAutoMappings)
			{
				var files = Directory.GetFiles(ResolveGamePath(map.TargetDirectory));
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
