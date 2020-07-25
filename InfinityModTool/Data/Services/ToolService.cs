using ElectronNET.API.Entities;
using InfinityModTool.Data;
using InfinityModTool.Data.Modifications;
using InfinityModTool.Data.Utilities;
using InfinityModTool.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Services
{
	public class ToolService
	{
		public async Task<bool> UnluacDecompileFolder(string folder)
		{
			try
			{
				await UnluacUtility.DecompileFolder(folder);
				return true;
			}
			catch (Exception ex)
			{
				return false;
			}
		}

		public async Task<bool> QuickBMSFolder(string folder, bool recursive)
		{
			try
			{
				foreach (var file in Directory.GetFiles(folder, "*.zip", SearchOption.AllDirectories))
				{
					var info = new FileInfo(file);
					await QuickBMSUtility.ExtractFiles(file, info.DirectoryName);
				}

				return true;
			}
			catch (Exception ex)
			{
				return false;
			}
		}
	}
}
