using System;
using System.Collections.Generic;
using System.Text;

namespace BitcoinCore
{
	public interface IHasForkId
	{
		uint ForkId
		{
			get;
		}
	}
}
