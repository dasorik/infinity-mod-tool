using System;
using System.Collections.Generic;

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

		static Queue<Log> logs = new Queue<Log>();

		public static void LogMessage(string message, LogSeverity severity)
		{
			lock (logs)
			{
				if (logs.Count == 2000)
					logs.Dequeue();

				logs.Enqueue(new Log(message, severity));
			}
		}

		public static IEnumerable<Log> GetLogs()
		{
			return logs;
		}
	}
}
