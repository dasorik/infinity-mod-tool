using InfinityModTool.Data;
using InfinityModTool.Data.InstallActions;
using InfinityModTool.Data.Modifications;
using InfinityModTool.Enums;
using InfinityModTool.Models;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Server.IIS.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace InfinityModTool.Utilities
{
	public class ModCollisionTracker
	{
		Configuration config;
		List<ModCollision> collisions = new List<ModCollision>();

		public ModCollisionTracker(Configuration config)
		{
			this.config = config;
		}

		public ModCollision[] CheckForPotentialModCollisions(GameModification newMod, ModActionCollection modActions)
		{
			var newModActions = modActions.CreateFilteredCollection(newMod, filterOut: false);
			var oldModActions = modActions.CreateFilteredCollection(newMod, filterOut: true);

			// We will use the assumption that all that have been installed previously were compatible with each other
			// (ie. we'll only check against the newly added mod)

			foreach (var fileWrite in newModActions.fileWriteActions)
			{
				// Are we attempting to write data to an overridden or deleted section of a file
				AddModClash(oldModActions.fileWriteActions, a => CheckForFileWriteCollisions(fileWrite, a), "A mod attempted to write content to a file at a position that is deleted/overriden by another");

				// Are we attempting to write to a deleted file?
				AddModClash(oldModActions.fileDeleteActions, a => fileWrite.action.TargetFile == a.action.TargetFile, "Attempting to write to a file that is deleted by another mod");

				// Are we attempting to write to a moved file, or the target destination?
				AddModClash(oldModActions.fileMoveActions, a => fileWrite.action.TargetFile == a.action.TargetFile, "Attempting to write to a file that is moved by another mod");

				// Are we attempting to write to a replaced file?
				AddModClash(oldModActions.fileReplaceActions, a => fileWrite.action.TargetFile == a.action.TargetFile, "Attempting to write to a file that is replaced by another mod");
			}

			foreach (var fileMove in newModActions.fileMoveActions)
			{
				// Are we attempting to move a writen to file?
				AddModClash(oldModActions.fileWriteActions, a => fileMove.action.TargetFile == a.action.TargetFile, "Attempting to move a file that is written to by another mod");

				// Are we attempting to move a file that is moved elsewhere?
				AddModClash(oldModActions.fileMoveActions, a => fileMove.action.TargetFile == a.action.TargetFile && fileMove.action.DestinationPath != a.action.DestinationPath, "Attempting to move a file that is moved elsewhere by another mod");

				// Are we attempting to move a file to a destination moved to by another move?
				AddModClash(oldModActions.fileMoveActions, a => fileMove.action.DestinationPath == a.action.DestinationPath, "Attempting to move a file to a destination that is moved to by another mod");

				// Are we attempting to move a deleted file? (probably safe)
				AddModWarning(oldModActions.fileDeleteActions, a => fileMove.action.TargetFile == a.action.TargetFile, "Attempting to move a file that is deleted by another mod");

				// Are we attempting to move to a replaced file?
				AddModClash(oldModActions.fileReplaceActions, a => fileMove.action.TargetFile == a.action.TargetFile, "Attempting to move a file that is replaced by another mod");

				// Are we attempting to move to a destination copied to by another mod?
				AddModClash(oldModActions.fileCopyActions, a => fileMove.action.DestinationPath == a.action.DestinationPath && fileMove.action.TargetFile != a.action.TargetFile, "Attempting to move a file to a destination that is copied to by another mod (with different data)");
			}

			foreach (var fileReplace in newModActions.fileReplaceActions)
			{
				// Are we attempting to replace a writen to file?
				AddModClash(oldModActions.fileWriteActions, a => fileReplace.action.TargetFile == a.action.TargetFile, "Attempting to replace a file that is written to by another mod");

				// Are we attempting to replace a deleted file?
				AddModClash(oldModActions.fileDeleteActions, a => fileReplace.action.TargetFile == a.action.TargetFile, "Attempting to replace a file that is deleted by another mod");

				// Are we attempting to replace to a moved file?
				AddModClash(oldModActions.fileMoveActions, a => fileReplace.action.TargetFile == a.action.TargetFile, "Attempting to replace a file that is moved by another mod");

				// Are we attempting to replace to a replaced file?
				AddModClash(oldModActions.fileReplaceActions, a => fileReplace.action.TargetFile == a.action.TargetFile && FilesAreDifferent(fileReplace, a), "Attempting to move a file that is replaced by another mod");
			}

			foreach (var fileDelete in newModActions.fileDeleteActions)
			{
				// Are we attempting to delete to a writen to file?
				AddModClash(oldModActions.fileWriteActions, a => fileDelete.action.TargetFile == a.action.TargetFile, "Attempting to delete a file that is written to by another mod");

				// Are we attempting to delete a file that is moved elsewhere?
				AddModClash(oldModActions.fileReplaceActions, a => fileDelete.action.TargetFile == a.action.TargetFile, "Attempting to delete a file that is replaced by another mod");

				// Are we attempting to delete to a moved file? (probably a safe thing to do)
				AddModWarning(oldModActions.fileMoveActions, a => fileDelete.action.TargetFile == a.action.TargetFile, "Attempting to delete a file that is moved by another mod");
			}

			foreach (var fileCopy in newModActions.fileCopyActions)
			{
				// Are we attempting to copy to a destination moved to by another mod
				AddModClash(oldModActions.fileMoveActions, a => fileCopy.action.DestinationPath == a.action.DestinationPath && fileCopy.action.TargetFile != a.action.TargetFile, "Attempting to copy a file to a destination that is moved to by another mod (with different data)");

				// Are we attempting to copy to a destination copied to by another mod
				AddModClash(oldModActions.fileCopyActions, a => fileCopy.action.DestinationPath == a.action.DestinationPath && fileCopy.action.TargetFile != a.action.TargetFile, "Attempting to copy a file to a destination that is copied to by another mod (with different data)");
			}

			return collisions.ToArray();
		}

		private void AddModWarning<T>(IEnumerable<ModAction<T>> actions, Func<ModAction<T>, bool> predicate, string message)
			where T : ModInstallAction
		{
			AddModCollisions(actions, predicate, ModCollisionSeverity.Warning, message);
		}

		private void AddModClash<T>(IEnumerable<ModAction<T>> actions, Func<ModAction<T>, bool> predicate, string message)
			where T : ModInstallAction
		{
			AddModCollisions(actions, predicate, ModCollisionSeverity.Clash, message);
		}

		private void AddModCollisions<T>(IEnumerable<ModAction<T>> actions, Func<ModAction<T>, bool> predicate, ModCollisionSeverity severity, string message)
			where T : ModInstallAction
		{
			foreach (var action in actions)
				if (predicate(action))
					collisions.Add(new ModCollision(action.mod, severity, message));
		}

		private bool CheckForFileWriteCollisions(ModAction<FileWriteAction> a1, ModAction<FileWriteAction> a2)
		{
			// These aren't targeting the same file, so ignore
			if (a1.action.TargetFile != a2.action.TargetFile)
				return false; 

			foreach(var a2Content in a2.action.Content)
			{
				var a2Length = a2Content.EndOffset.HasValue ? a2Content.EndOffset.Value - a2Content.StartOffset : 0;

				foreach (var a1Content in a1.action.Content)
				{
					var a1Length = a1Content.EndOffset.HasValue ? a1Content.EndOffset.Value - a1Content.StartOffset : 0;

					// Should not cause collisions
					if (!a1Content.Replace && !a2Content.Replace)
						continue;

					// Current mod is trying to replace a section injected into by another
					if (a1Content.Replace && a2Content.StartOffset > a1Content.StartOffset && a2Content.StartOffset < a1Content.EndOffset.Value)
						return true;

					// Other mod is trying to replace a section injected into by the current mod
					if (a2Content.Replace && a1Content.StartOffset > a2Content.StartOffset && a1Content.StartOffset < a2Content.EndOffset.Value)
						return true;
				}
			}

			return false;
		}

		private bool FilesAreDifferent(ModAction<FileReplaceAction> a1, ModAction<FileReplaceAction> a2)
		{
			var a1FilePath = ModUtility.ResolvePath(a1.action.TargetFile, a1.mod, config);
			var a2FilePath = ModUtility.ResolvePath(a2.action.TargetFile, a2.mod, config);

			string a1MD5 = MD5Utility.CalculateMD5Hash(a1FilePath);
			string a2MD5 = MD5Utility.CalculateMD5Hash(a2FilePath);

			return a1MD5 != a2MD5;
		}
	}
}

