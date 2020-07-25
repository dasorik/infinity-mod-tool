using InfinityModTool.Data.InstallActions;
using InfinityModTool.Data.Modifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
		public List<ModAction<QuickBMSExtractAction>> extractActions;
		public List<ModAction<UnluacDecompileAction>> decompileActions;
		public List<ModAction<FileMoveAction>> fileMoveActions;
		public List<ModAction<FileDeleteAction>> fileDeleteActions;
		public List<ModAction<FileWriteAction>> fileWriteActions;
		public List<ModAction<FileReplaceAction>> fileReplaceActions;
		public List<ModAction<FileCopyAction>> fileCopyActions;

		public ModActionCollection CreateFilteredCollection(GameModification mod, bool filterOut = false)
		{
			return new ModActionCollection()
			{
				extractActions = extractActions.Where(a => filterOut ? !a.mod.Equals(mod) : a.mod.Equals(mod)).ToList(),
				decompileActions = decompileActions.Where(a => filterOut ? !a.mod.Equals(mod) : a.mod.Equals(mod)).ToList(),
				fileMoveActions = fileMoveActions.Where(a => filterOut ? !a.mod.Equals(mod) : a.mod.Equals(mod)).ToList(),
				fileDeleteActions = fileDeleteActions.Where(a => filterOut ? !a.mod.Equals(mod) : a.mod.Equals(mod)).ToList(),
				fileWriteActions = fileWriteActions.Where(a => filterOut ? !a.mod.Equals(mod) : a.mod.Equals(mod)).ToList(),
				fileReplaceActions = fileReplaceActions.Where(a => filterOut ? !a.mod.Equals(mod) : a.mod.Equals(mod)).ToList(),
				fileCopyActions = fileCopyActions.Where(a => filterOut ? !a.mod.Equals(mod) : a.mod.Equals(mod)).ToList(),
			};
		}
	}
}
