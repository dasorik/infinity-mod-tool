using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Extension
{
	public static class StringExtensions
	{
		public static bool SafeEquals(this string text, string comparison, bool ignoreCase = false, bool nullsAreEqual = false)
		{
			if (text == null)
				return nullsAreEqual ? comparison == null : false;

			if (ignoreCase)
				return text.Equals(comparison, StringComparison.InvariantCultureIgnoreCase);
			else
				return text.Equals(comparison);
		}
	}
}
