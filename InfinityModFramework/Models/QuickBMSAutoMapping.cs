using InfinityModFramework.InstallActions;

namespace InfinityModFramework.Models
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
