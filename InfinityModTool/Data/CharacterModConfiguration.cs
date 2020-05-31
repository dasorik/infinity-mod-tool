using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Data
{
	public class CharacterModConfiguration : BaseModConfiguration
	{
		public bool ReplaceCharacter;
		public string ReplaceCharacterName;
		public bool WriteToCharacterList;
		public CharacterData PresentationData;
	}
}
