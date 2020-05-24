using Microsoft.Extensions.Configuration;
using LitJson;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace InfinityModTool.Data.Utilities
{
	public class ModLoaderService
	{
#if DEBUG
		const string MOD_PATH_CHARACTER = "..\\..\\..\\Mods\\Characters";
#else
		const string MOD_PATH_CHARACTER = "Mods\\Characters";
#endif

		public static CharacterData[] GetAvailableCharacterMods()
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var characterModPath = Path.Combine(executionPath, MOD_PATH_CHARACTER);

			if (!Directory.Exists(characterModPath))
				Directory.CreateDirectory(characterModPath);

			var characterDataList = new List<CharacterData>();

			foreach (var file in Directory.GetFiles(characterModPath))
			{
				if (new FileInfo(file).Extension == ".json")
				{
					var fileData = File.ReadAllText(file);
					var characterData = JsonMapper.ToObject<CharacterData>(fileData);

					characterDataList.Add(characterData);
				}
			}

			return characterDataList.ToArray();
		}
	}
}
