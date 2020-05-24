using Microsoft.Extensions.Configuration;
using LitJson;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;

namespace InfinityModTool.Data.Utilities
{
	public class ModLoaderService
	{
#if DEBUG
		const string MOD_PATH_CHARACTER = "..\\..\\..\\Mods\\Characters";
		const string CHARACTER_ID_NAMES = "..\\..\\..\\character_ids.json";
#else
		const string MOD_PATH_CHARACTER = "Mods\\Characters";
		const string CHARACTER_ID_NAMES = "character_ids.json";
#endif

		public static ListOption[] GetIDNameListOptions()
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var idNamePath = Path.Combine(executionPath, CHARACTER_ID_NAMES);

			var fileData = File.ReadAllText(idNamePath);
			var idNames = JsonMapper.ToObject<IDNames>(fileData);

			return idNames.CharacterIDs.Select(id => new ListOption(id.ID, id.DisplayName)).ToArray();
		}

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
