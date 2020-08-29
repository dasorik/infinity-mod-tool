using InfinityModFramework.InstallActions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModFramework.Models
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
		public string ModCachePath;
		public string DisplayImageBase64;
		public ModInstallAction[] InstallActions;
	}
}
