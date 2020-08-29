using InfinityModFramework;
using InfinityModFramework.Interfaces;
using InfinityModFramework.Models;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace Integrations
{
	public class DisneyInfinityIntegration : IGameIntegration
	{
#if DEBUG
		const string RESERVED_FILES = "..\\..\\..\\reserved.txt";
		const string AUTO_MAPPING_PATH = "..\\..\\..\\automapping.json";
#else
        const string RESERVED_FILES = "reserved.txt";
        const string AUTO_MAPPING_PATH = "automapping.json";
#endif

		public QuickBMSAutoMappingCollection GetQuickBMSAutomappings()
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var filePath = Path.Combine(executionPath, AUTO_MAPPING_PATH);
			var json = File.ReadAllText(filePath);

			return JsonConvert.DeserializeObject<QuickBMSAutoMappingCollection>(json, new ModInstallActionConverter());
		}

		public string[] GetReservedFiles()
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var filePath = Path.Combine(executionPath, RESERVED_FILES);

			return File.ReadAllLines(filePath);
		}
	}
}
