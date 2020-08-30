using InfinityModEngine.InstallActions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModEngine.Models
{
	public class BaseModConfiguration
	{
		public double Version;
		public string ModID;
		public string ModCategory;
		public string Description;
		public string DisplayImage;
		public string DisplayColor;
		public string DisplayName;
		public ModInstallAction[] InstallActions;

		// Cached data
		[JsonIgnore] public string DisplayImageBase64;
		[JsonIgnore] public string CacheFolderName;
	}
}
