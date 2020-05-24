using System.IO;
using System.Reflection;
using LitJson;

namespace InfinityModTool.Data.Utilities
{
	public class SettingsUtility
	{
#if DEBUG
		const string SETTINGS_PATH = "..\\..\\..\\..\\Data";
#else
		const string SETTINGS_PATH = "Data";
#endif

		public static void SaveSettings<T>(T settings, string name)
		{
			var dataPath = GetSettingsPath(name);
			var dataDirectory = Path.GetDirectoryName(dataPath);

			var json = JsonMapper.ToJson(settings);

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
			return JsonMapper.ToObject<T>(jsonString);
		}

		static string GetSettingsPath(string name)
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			return Path.Combine(executionPath, SETTINGS_PATH, $"{name}.json");
		}
	}
}
