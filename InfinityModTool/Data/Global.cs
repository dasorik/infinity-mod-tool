using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Data
{
	public class Global
	{
		public static readonly string APP_DATA_FOLDER;

		static Global()
		{
			string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			APP_DATA_FOLDER = Path.Combine(appDataPath, "InfinityModTool");

			if (!Directory.Exists(APP_DATA_FOLDER))
				Directory.CreateDirectory(APP_DATA_FOLDER);
		}
	}
}
