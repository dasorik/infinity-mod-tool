using InfinityModEngine.InstallActions;

namespace InfinityModEngine.Models
{
	public class GameIntegration
	{
		public ModInstallAction[] SetupActions;
		public ModInstallAction[] PreInstallActions;
		public ModInstallAction[] PostInstallActions;
		public QuickBMSAutoMapping[] QuickBMSAutoMappings;
	}
}
