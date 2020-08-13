using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;

namespace InfinityModTool.Utilities
{
	public class Logging
	{
		public enum LogSeverity
		{
			Info,
			Warning,
			Error
		}

		public struct Log
		{
			public readonly DateTime date;
			public readonly string message;
			public readonly LogSeverity severity;

			public Log(string message, LogSeverity severity)
			{
				this.message = message;
				this.severity = severity;
				this.date = DateTime.Now;
			}
		}

		static string logFile;
		static Queue<Log> logs = new Queue<Log>();

		static Logging()
		{
			var logDirectory = Path.Combine(Data.Global.APP_DATA_FOLDER, "Logs");
			logFile = Path.Combine(logDirectory, $"log_{DateTime.Now.ToString("yyyyMMddhhss")}.txt");

			Directory.CreateDirectory(logDirectory);
		}

		public static void LogMessage(string message, LogSeverity severity)
		{
			lock (logs)
			{
				if (logs.Count == 2000)
					logs.Dequeue();

				logs.Enqueue(new Log(message, severity));
				File.AppendAllText(logFile, $"{GetLogPrefix(severity)} {message}\n");
			}
		}

		static string GetLogPrefix(LogSeverity severity)
		{
			switch (severity)
			{
				case LogSeverity.Info:
					return "[INFO]    ";
				case LogSeverity.Warning:
					return "[WARNING] ";
				case LogSeverity.Error:
					return "[ERROR]   ";
			}

			return string.Empty;
		}

		public static IEnumerable<Log> GetLogs()
		{
			return logs;
		}
	}
}
