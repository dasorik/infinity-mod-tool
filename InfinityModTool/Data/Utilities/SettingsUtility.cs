using System.IO;
using Newtonsoft.Json;

namespace InfinityModTool.Data.Utilities
{
	public class SettingsUtility
	{
		public static void SaveSettings<T>(T settings, string name)
		{
			var dataPath = GetSettingsPath(name);
			var dataDirectory = Path.GetDirectoryName(dataPath);

			var json = JsonConvert.SerializeObject(settings);

			if (!Directory.Exists(dataDirectory))
				Directory.CreateDirectory(dataDirectory);

			File.WriteAllText(dataPath, json);
		}

		public static T LoadSettings<T>(string name)
			where T : new()
		{
			var dataPath = GetSettingsPath(name);

			if (!File.Exists(dataPath))
			{
				var settings = new T();
				SaveSettings(settings, name);
				return settings;
			}

			var jsonString = File.ReadAllText(dataPath);
			return JsonConvert.DeserializeObject<T>(jsonString);
		}

		static string GetSettingsPath(string name)
		{
			return Path.Combine(Global.APP_DATA_FOLDER, $"{name}.json");
		}
	}
}
