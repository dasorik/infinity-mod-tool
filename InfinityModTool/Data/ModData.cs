using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Data
{
	public class CharacterModLink
	{
		public string CharacterID;
		public string ReplacementCharacterID;
	}

	public class UserModData
	{
		public string SteamInstallationPath;
		public List<CharacterModLink> InstalledCharacterMods = new List<CharacterModLink>();
	}
}
