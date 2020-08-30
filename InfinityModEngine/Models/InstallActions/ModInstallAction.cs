
using System.Text.Json.Serialization;

namespace InfinityModEngine.InstallActions
{
	[JsonConverter(typeof(ModInstallActionConverter))]
	public class ModInstallAction
	{
		public string Action;
	}
}
