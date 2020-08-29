
using InfinityModFramework.Interfaces;
using InfinityModFramework.Models;

namespace InfinityModFramework.InstallActions
{
	public class WriteToFileAction : ModInstallAction
	{
		public string TargetFile;
		public WriteContent[] Content;
	}
}
