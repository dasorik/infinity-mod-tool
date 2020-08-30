using InfinityModEngine.Models;
using System.Collections.Generic;

namespace InfinityModEngine
{
	public class Configuration
	{
		public string TargetPath;
		public string CacheFolder;
		public string TempFolder;
		public string BackupFolder;
		public string ToolPath;
		public IEnumerable<FileModification> Modifications;
	}
}
