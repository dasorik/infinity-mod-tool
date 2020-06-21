using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Data
{
	public class CostumeCoinModConfiguration : BaseModConfiguration
	{
		public string TargetCharacterID;
		public bool WriteToCharacterList;
		public bool WriteToCostumeCoinList;
		public CostumeCoinData PresentationData;
	}
}
