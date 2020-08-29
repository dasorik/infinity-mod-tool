
using System.Text.Json.Serialization;

namespace InfinityModFramework.InstallActions
{
	[JsonConverter(typeof(ModInstallActionConverter))]
	public class ModInstallAction
	{
		public string Action;
	}
}
