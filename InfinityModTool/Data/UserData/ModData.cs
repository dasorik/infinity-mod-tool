using InfinityModFramework.Models;
using System.Collections.Generic;

namespace InfinityModTool.Models
{
	public class ModInstallationData
	{
		public string ModID;
		public Dictionary<string, string> Parameters = new Dictionary<string, string>();
	}

	public class UserModData
	{
		public string SteamInstallationPath;
		public bool ModdingToolsEnabled;
		public List<string> AvailableMods = new List<string>();
		public List<ModInstallationData> InstalledMods = new List<ModInstallationData>();
		public readonly List<FileModification> FileModifications = new List<FileModification>();
	}
}
