using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Data.InstallActions
{
	public class ReplaceFilesAction : ModInstallAction
	{
		public string TargetDirectory;
		public string FileFilter;
		public bool IncludeSubfolders;
		public string DestinationPath;
	}
}
