using InfinityModTool.Extension;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnluacNET;

namespace InfinityModTool.Utilities
{
	public class UnluacUtility
	{
		public static async Task Decompile(string inputPath, string outputPath)
		{
			await Task.Run(() => DecompileSync(inputPath, outputPath));
		}

		private static void DecompileSync(string inputPath, string outputPath)
		{
			Console.WriteLine($"Decompiling file {inputPath} with Unluac.Net");

			LFunction lMain = null;

			try
			{
				lMain = FileToFunction(inputPath);
			}
			catch (Exception ex)
			{
				Console.Write($"[ERROR - UNLUAC.NET]: {ex}");
				return;
			}

			var d = new Decompiler(lMain);
			d.Decompile();

			try
			{
				using (var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false)))
				{
					d.Print(new Output(writer));
					writer.Flush();

					Console.WriteLine($"Successfully decompiled to '{outputPath}'");
				}
			}
			catch (Exception ex)
			{
				Console.Write($"[ERROR - UNLUAC.NET]: {ex}");
				return;
			}
		}

		private static LFunction FileToFunction(string fn)
		{
			using (var fs = File.Open(fn, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				var header = new BHeader(fs);
				return header.Function.Parse(fs, header);
			}
		}
	}
}
