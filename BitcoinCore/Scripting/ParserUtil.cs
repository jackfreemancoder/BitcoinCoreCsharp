using System.Collections.Generic;
using BitcoinCore.Scripting.Parser;
using System;

namespace BitcoinCore.Scripting
{
	internal static class ParserUtil
	{
		internal static readonly Parser<char, string> SurroundedByBrackets =
				from leftB in Parse.Char('(')
				from x in Parse.CharExcept(')').Many().Text()
				from rightB in Parse.Char(')')
				select x;
	}
}
