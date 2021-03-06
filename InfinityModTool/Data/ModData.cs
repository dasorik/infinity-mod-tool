﻿using InfinityModTool.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Data
{
	public class ModInstallationData
	{
		public string ModID;
		public string ModCategory;
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
