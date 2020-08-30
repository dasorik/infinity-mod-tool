
using InfinityModEngine.Interfaces;
using InfinityModEngine.Models;

namespace InfinityModEngine.InstallActions
{
	public class WriteToFileAction : ModInstallAction
	{
		public string TargetFile;
		public WriteContent[] Content;
	}
}
