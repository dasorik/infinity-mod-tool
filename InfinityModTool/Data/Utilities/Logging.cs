using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Utilities
{
	public class Logging
	{
		public struct Log
		{
			public readonly DateTime date;
			public readonly string message;
			public readonly DiagnosticSeverity severity;

			public Log(string message, DiagnosticSeverity severity)
			{
				this.message = message;
				this.severity = severity;
				this.date = DateTime.Now;
			}
		}

		static Queue<Log> logs = new Queue<Log>();

		public static void LogMessage(string message, DiagnosticSeverity severity)
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
