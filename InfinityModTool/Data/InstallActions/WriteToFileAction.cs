using InfinityModTool.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Data.InstallActions
{
	public class WriteToFileAction : ModInstallAction
	{
		public string TargetFile;
		public WriteContent[] Content;

		public class WriteContent : IWriteContent
		{
			public long StartOffset;
			public long? EndOffset;
			public string DataFilePath;
			public string Text;
			public bool Replace;

			long IWriteContent.StartOffset => StartOffset;
			long? IWriteContent.EndOffset => EndOffset;
			string IWriteContent.DataFilePath => DataFilePath;
			string IWriteContent.Text => Text;
			bool IWriteContent.Replace => Replace;
		}
	}
}
