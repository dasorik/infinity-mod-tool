using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Data.InstallActions
{
	public class FileWriteAction : ModInstallAction
	{
		public string TargetFile;
		public WriteContent[] Content;

		public class WriteContent
		{
			public long StartOffset;
			public long? EndOffset;
			public string DataFilePath;
			public string Text;
			public bool Replace;
		}
	}
}
