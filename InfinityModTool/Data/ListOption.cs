using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Data
{
	public struct ListOption
	{
		public string Value { get; }
		public string Name { get; }

		public ListOption(string value, string name)
		{
			this.Value = value;
			this.Name = name;
		}
	}
}
