using InfinityModTool.Data.InstallActions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Data.Models
{
	public class QuickBMSAutoMapping
	{
		public string TargetDirectory;
		public string FileFilter;
		public ModInstallAction[] Actions;
	}

	public class QuickBMSAutoMappingCollection
	{
		public QuickBMSAutoMapping[] QuickBMSAutoMappings;
	}
}
