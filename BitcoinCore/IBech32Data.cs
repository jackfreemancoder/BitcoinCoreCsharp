﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinCore
{
	public interface IBech32Data : IBitcoinString
	{
		Bech32Type Type
		{
			get;
		}
	}
}
