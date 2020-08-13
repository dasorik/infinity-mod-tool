using InfinityModTool.Data;
using InfinityModTool.Data.InstallActions;
using InfinityModTool.Data.Modifications;
using InfinityModTool.Enums;
using InfinityModTool.Models;
using InfinityModTool.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace InfinityModTool.Utilities
{
	public class ModCollisionTracker
	{
		private class ActionCollection
		{
			public readonly FileModification moveAction;
			public readonly FileModification replaceAction;
			public readonly FileModification editAction;
			public readonly FileModification addAction;
			public readonly FileModification deleteAction;
			public readonly FileModification lastAction;

			public ActionCollection(string file, string currentModID, List<FileModification> modifications)
			{
				var fileModifications = modifications.Where(m => m.FilePath == file);

				this.moveAction = fileModifications.LastOrDefault(a => a.ModID != currentModID && a.Type == FileModificationType.Moved);
				this.replaceAction = fileModifications.LastOrDefault(a => a.ModID != currentModID && a.Type == FileModificationType.Replaced);
				this.editAction = fileModifications.LastOrDefault(a => a.ModID != currentModID && a.Type == FileModificationType.Edited);
				this.addAction = fileModifications.LastOrDefault(a => a.ModID != currentModID && a.Type == FileModificationType.Added);
				this.deleteAction = fileModifications.LastOrDefault(a => a.ModID != currentModID && a.Type == FileModificationType.Deleted);
				this.lastAction = fileModifications.LastOrDefault(a => a.ModID != currentModID);
			}
		}

		public static bool HasMoveCollision(GameModification currentMod, string file, string destinationPath, List<FileModification> modifications, out ModCollision collision)
		{
			var actions = new ActionCollection(file, currentMod.Config.ModID, modifications);
			var destinationActions = new ActionCollection(destinationPath, currentMod.Config.ModID, modifications);

			if (actions.moveAction != null && destinationPath != (actions.moveAction as MoveFileModification).DestinationPath)
				return AddModCollision(currentMod, ModInstallActionEnum.Move, FileModificationType.Moved, actions.moveAction.ModID, ModCollisionSeverity.Clash, out collision);

			if (actions.replaceAction != null)
				return AddModCollision(currentMod, ModInstallActionEnum.Move, FileModificationType.Replaced, actions.replaceAction.ModID, ModCollisionSeverity.Clash, out collision);

			if (actions.editAction != null)
				return AddModCollision(currentMod, ModInstallActionEnum.Move, FileModificationType.Edited, actions.editAction.ModID, ModCollisionSeverity.Clash, out collision);

			if (actions.deleteAction != null)
				return AddModCollision(currentMod, ModInstallActionEnum.Move, FileModificationType.Deleted, actions.deleteAction.ModID, ModCollisionSeverity.Clash, out collision);

			if (destinationActions.lastAction != null && !(new[] { FileModificationType.Moved, FileModificationType.Deleted }).Contains(destinationActions.lastAction.Type))
			{
				if (MD5Utility.CalculateMD5Hash(file) != MD5Utility.CalculateMD5Hash(destinationPath))
				{
					string modPrefix = $"Mod collision detected while installing mod ({currentMod.Config.ModID})";
					collision = new ModCollision(destinationActions.lastAction.ModID, ModCollisionSeverity.Clash, $"{modPrefix}: Attempting to move a file to a destination that has been modified by another mod (with different data) (conflicting mod: {destinationActions.lastAction.ModID})");
					return true;
				}
			}

			collision = null;
			return false;
		}

		public static bool HasCopyCollision(GameModification currentMod, string file, string destinationPath, List<FileModification> modifications, out ModCollision collision)
		{
			var actions = new ActionCollection(file, currentMod.Config.ModID, modifications);
			var destinationActions = new ActionCollection(destinationPath, currentMod.Config.ModID, modifications);

			if (actions.moveAction != null && destinationPath != (actions.moveAction as MoveFileModification).DestinationPath)
				return AddModCollision(currentMod, ModInstallActionEnum.Copy, FileModificationType.Moved, actions.moveAction.ModID, ModCollisionSeverity.Clash, out collision);

			if (actions.replaceAction != null)
				return AddModCollision(currentMod, ModInstallActionEnum.Copy, FileModificationType.Replaced, actions.replaceAction.ModID, ModCollisionSeverity.Clash, out collision);

			if (actions.editAction != null)
				return AddModCollision(currentMod, ModInstallActionEnum.Copy, FileModificationType.Edited, actions.editAction.ModID, ModCollisionSeverity.Clash, out collision);

			if (actions.deleteAction != null)
				return AddModCollision(currentMod, ModInstallActionEnum.Copy, FileModificationType.Deleted, actions.deleteAction.ModID, ModCollisionSeverity.Clash, out collision);

			if (destinationActions.lastAction != null && !(new[] { FileModificationType.Moved, FileModificationType.Deleted }).Contains(destinationActions.lastAction.Type))
			{
				if (MD5Utility.CalculateMD5Hash(file) != MD5Utility.CalculateMD5Hash(destinationPath))
				{
					string modPrefix = $"Mod collision detected while installing mod ({currentMod.Config.ModID})";
					collision = new ModCollision(currentMod.Config.ModID, ModCollisionSeverity.Clash, $"{modPrefix}: Attempting to copy a file to a destination that has been modified by another mod (with different data) (conflicting mod: {destinationActions.lastAction.ModID})");
					return true;
				}
			}

			collision = null;
			return false;
		}

		public static bool HasReplaceCollision(GameModification currentMod, string file, string replacementFile, List<FileModification> modifications, out ModCollision collision)
		{
			var actions = new ActionCollection(file, currentMod.Config.ModID, modifications);

			if (actions.moveAction != null)
				return AddModCollision(currentMod, ModInstallActionEnum.Replace, FileModificationType.Moved, actions.moveAction.ModID, ModCollisionSeverity.Clash, out collision);

			if (actions.replaceAction != null && MD5Utility.CalculateMD5Hash(file) != MD5Utility.CalculateMD5Hash(replacementFile))
				return AddModCollision(currentMod, ModInstallActionEnum.Replace, FileModificationType.Replaced, actions.replaceAction.ModID, ModCollisionSeverity.Clash, out collision, suffix: "(with different data)");

			if (actions.editAction != null)
				return AddModCollision(currentMod, ModInstallActionEnum.Replace, FileModificationType.Edited, actions.editAction.ModID, ModCollisionSeverity.Clash, out collision);

			if (actions.deleteAction != null)
				return AddModCollision(currentMod, ModInstallActionEnum.Replace, FileModificationType.Deleted, actions.deleteAction.ModID, ModCollisionSeverity.Clash, out collision);

			collision = null;
			return false;
		}

		public static bool HasEditCollision(GameModification currentMod, string file, IWriteContent content, FileWriterUtility fileWriter, List<FileModification> modifications, out ModCollision collision)
		{
			var actions = new ActionCollection(file, currentMod.Config.ModID, modifications);

			if (actions.moveAction != null)
				return AddModCollision(currentMod, ModInstallActionEnum.Edit, FileModificationType.Moved, actions.moveAction.ModID, ModCollisionSeverity.Clash, out collision);

			if (actions.replaceAction != null)
				return AddModCollision(currentMod, ModInstallActionEnum.Edit, FileModificationType.Replaced, actions.replaceAction.ModID, ModCollisionSeverity.Clash, out collision);

			if (actions.editAction != null && !fileWriter.CanWrite(file, content))
				return AddModCollision(currentMod, ModInstallActionEnum.Edit, FileModificationType.Edited, actions.editAction.ModID, ModCollisionSeverity.Clash, out collision);

			if (actions.deleteAction != null)
				return AddModCollision(currentMod, ModInstallActionEnum.Edit, FileModificationType.Deleted, actions.deleteAction.ModID, ModCollisionSeverity.Clash, out collision);

			collision = null;
			return false;
		}

		private static bool AddModCollision(GameModification mod, ModInstallActionEnum action, FileModificationType collisionReason, string collidingModID, ModCollisionSeverity severity, out ModCollision collision, string suffix = "")
		{
			string collisionReasonDescription = GetCollisionDescription(collisionReason);
			string actionDescription = GetModificationDescription(action);

			return AddModCollision(mod, actionDescription, collisionReasonDescription, collidingModID, severity, out collision, suffix: suffix);
		}

		private static bool AddModCollision(GameModification mod, string actionDescription, FileModificationType collisionReason, string collidingModID, ModCollisionSeverity severity, out ModCollision collision, string suffix = "")
		{
			string collisionReasonDescription = GetCollisionDescription(collisionReason);
			return AddModCollision(mod, actionDescription, collisionReasonDescription, collidingModID, severity, out collision, suffix: suffix);
		}

		private static bool AddModCollision(GameModification mod, ModInstallActionEnum action, string collisionReasonDescription, string collidingModID, ModCollisionSeverity severity, out ModCollision collision, string suffix = "")
		{
			string actionDescription = GetModificationDescription(action);
			return AddModCollision(mod, actionDescription, collisionReasonDescription, collidingModID, severity, out collision, suffix: suffix);
		}

		private static bool AddModCollision(GameModification mod, string actionDescription, string collisionReasonDescription, string collidingModID, ModCollisionSeverity severity, out ModCollision collision, string suffix = "")
		{
			string modPrefix = $"Mod collision detected while installing mod ({mod.Config.ModID})";

			collision = new ModCollision(collidingModID, severity, $"{modPrefix}: Attempting to {actionDescription} that has been {collisionReasonDescription} another mod{(string.IsNullOrEmpty(suffix) ? "" : $" {suffix}")} (conflicting mod: {collidingModID})");
			return true;
		}

		private static string GetModificationDescription(ModInstallActionEnum action)
		{
			switch (action)
			{
				case ModInstallActionEnum.Copy:
					return "copy a file";
				case ModInstallActionEnum.Delete:
					return "delete a file";
				case ModInstallActionEnum.Edit:
					return "write to a file";
				case ModInstallActionEnum.Move:
					return "move a file";
				case ModInstallActionEnum.Replace:
					return "replace a file";
				case ModInstallActionEnum.QuickBMS:
					return "QuickBMS extract a file";
			}

			return "perform unknown action to a file";
		}

		private static string GetCollisionDescription(FileModificationType collisionReason)
		{
			switch (collisionReason)
			{
				case FileModificationType.Added:
					return "added by";
				case FileModificationType.Deleted:
					return "deleted by";
				case FileModificationType.Edited:
					return "written to by";
				case FileModificationType.Moved:
					return "moved by";
				case FileModificationType.Replaced:
					return "replaced by";
				case FileModificationType.QuickBMSExtracted:
					return "QuickBMS extracted by";
			}

			return "perform unknown action to a file";
		}
	}
}

