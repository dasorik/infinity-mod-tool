using InfinityModTool.Extension;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace InfinityModTool.Utilities
{
	public class QuickBMSUtility
	{
#if DEBUG
		const string TOOL_PATH = "..\\..\\..\\Tools";
#else
		const string TOOL_PATH = "Tools";
#endif

		public static async Task ExtractFiles(string inputPath, string outputPath)
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var quickBmsPath = Path.Combine(executionPath, TOOL_PATH, "quickbms\\quickbms.exe");
			var scriptPath = Path.Combine(executionPath, TOOL_PATH, "disney_infinity.bms");

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
