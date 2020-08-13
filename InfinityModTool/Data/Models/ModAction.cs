using InfinityModTool.Data.InstallActions;
using InfinityModTool.Data.Modifications;
using InfinityModTool.Extension;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace InfinityModTool.Models
{
	public class ModAction<T>
	{
		public readonly GameModification mod;
		public readonly T action;

		public ModAction(GameModification mod, T action)
		{
			this.mod = mod;
			this.action = action;
		}
	}

	public class ModActionCollection
	{
		public List<ModAction<QuickBMSExtractAction>> extractActions = new List<ModAction<QuickBMSExtractAction>>();
		public List<ModAction<UnluacDecompileAction>> decompileActions = new List<ModAction<UnluacDecompileAction>>();
		public List<ModAction<MoveFileAction>> fileMoveActions = new List<ModAction<MoveFileAction>>();
		public List<ModAction<MoveFilesAction>> bulkFileMoveActions = new List<ModAction<MoveFilesAction>>();
		public List<ModAction<DeleteFilesAction>> fileDeleteActions = new List<ModAction<DeleteFilesAction>>();
		public List<ModAction<WriteToFileAction>> fileWriteActions = new List<ModAction<WriteToFileAction>>();
		public List<ModAction<ReplaceFileAction>> fileReplaceActions = new List<ModAction<ReplaceFileAction>>();
		public List<ModAction<ReplaceFilesAction>> bulkFileReplaceActions = new List<ModAction<ReplaceFilesAction>>();
		public List<ModAction<CopyFileAction>> fileCopyActions = new List<ModAction<CopyFileAction>>();
		public List<ModAction<CopyFilesAction>> bulkFileCopyActions = new List<ModAction<CopyFilesAction>>();

		public void AddActionsFromMod(GameModification mod)
		{
			var installActions = (mod.Config.InstallActions ?? new ModInstallAction[] { });
			AddActions(mod, installActions);
		}

		public void AddActions(GameModification mod, ModInstallAction[] actions)
		{
			extractActions.AddRange(actions.Where(a => a.Action.SafeEquals("QuickBMSExtract", ignoreCase: true)).Select(a => new ModAction<QuickBMSExtractAction>(mod, a as QuickBMSExtractAction)));
			decompileActions.AddRange(actions.Where(a => a.Action.SafeEquals("UnluacDecompile", ignoreCase: true)).Select(a => new ModAction<UnluacDecompileAction>(mod, a as UnluacDecompileAction)));
			fileMoveActions.AddRange(actions.Where(a => a.Action.SafeEquals("MoveFile", ignoreCase: true)).Select(a => new ModAction<MoveFileAction>(mod, a as MoveFileAction)));
			bulkFileMoveActions.AddRange(actions.Where(a => a.Action.SafeEquals("MoveFiles", ignoreCase: true)).Select(a => new ModAction<MoveFilesAction>(mod, a as MoveFilesAction)));
			fileDeleteActions.AddRange(actions.Where(a => a.Action.SafeEquals("DeleteFiles", ignoreCase: true)).Select(a => new ModAction<DeleteFilesAction>(mod, a as DeleteFilesAction)));
			fileWriteActions.AddRange(actions.Where(a => a.Action.SafeEquals("WriteToFile", ignoreCase: true)).Select(a => new ModAction<WriteToFileAction>(mod, a as WriteToFileAction)));
			fileReplaceActions.AddRange(actions.Where(a => a.Action.SafeEquals("ReplaceFile", ignoreCase: true)).Select(a => new ModAction<ReplaceFileAction>(mod, a as ReplaceFileAction)));
			bulkFileReplaceActions.AddRange(actions.Where(a => a.Action.SafeEquals("ReplaceFiles", ignoreCase: true)).Select(a => new ModAction<ReplaceFilesAction>(mod, a as ReplaceFilesAction)));
			fileCopyActions.AddRange(actions.Where(a => a.Action.SafeEquals("CopyFile", ignoreCase: true)).Select(a => new ModAction<CopyFileAction>(mod, a as CopyFileAction)));
			bulkFileCopyActions.AddRange(actions.Where(a => a.Action.SafeEquals("CopyFiles", ignoreCase: true)).Select(a => new ModAction<CopyFilesAction>(mod, a as CopyFilesAction)));
		}

		public ModActionCollection CreateFilteredCollection(GameModification mod, bool filterOut = false)
		{
			return new ModActionCollection()
			{
				extractActions = extractActions.Where(a => Filter(a, mod, filterOut)).ToList(),
				decompileActions = decompileActions.Where(a => Filter(a, mod, filterOut)).ToList(),
				fileMoveActions = fileMoveActions.Where(a => Filter(a, mod, filterOut)).ToList(),
				bulkFileMoveActions = bulkFileMoveActions.Where(a => Filter(a, mod, filterOut)).ToList(),
				fileDeleteActions = fileDeleteActions.Where(a => Filter(a, mod, filterOut)).ToList(),
				fileWriteActions = fileWriteActions.Where(a => Filter(a, mod, filterOut)).ToList(),
				fileReplaceActions = fileReplaceActions.Where(a => Filter(a, mod, filterOut)).ToList(),
				bulkFileReplaceActions = bulkFileReplaceActions.Where(a => Filter(a, mod, filterOut)).ToList(),
				fileCopyActions = fileCopyActions.Where(a => Filter(a, mod, filterOut)).ToList()
			};
		}

		private bool Filter<TAction>(ModAction<TAction> action, GameModification mod, bool filterOut = false)
		{
			return filterOut ? !action.mod.Equals(mod) : action.mod.Equals(mod);
		}
	}
}
