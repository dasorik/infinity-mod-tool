using InfinityModEngine.Extension;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace InfinityModEngine.Utilities
{
	public class QuickBMSUtility
	{
		public static async Task ExtractFiles(string inputPath, string outputPath, string toolPath)
		{
			var quickBmsPath = Path.Combine(toolPath, "quickbms\\quickbms.exe");
			var scriptPath = Path.Combine(toolPath, "disney_infinity.bms");

			Console.WriteLine($"Extracting file {inputPath} with QuickBMS");

			try
			{
				using (var quickBms = Process.Start(quickBmsPath, $"\"{scriptPath}\" \"{inputPath}\" \"{outputPath}\""))
				{
					await quickBms.WaitForExitAsync();
				}
			}
			catch (Exception ex)
			{
				Console.Write($"[ERROR - QUICKBMS]: {ex}");
			}
		}
	}
}
