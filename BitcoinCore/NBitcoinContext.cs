﻿#if HAS_SPAN
using BitcoinCore.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace BitcoinCore
{
	internal class BitcoinCoreContext
	{
		static readonly Lazy<Context> _Instance = new Lazy<Context>(CreateInstance, true);
		public static Context Instance => _Instance.Value;
		static Context CreateInstance()
		{
			var gen = new ECMultGenContext();
			gen.Blind(RandomUtils.GetBytes(32));
			return new Context(new ECMultContext(), gen);
		}
	}
}
#endif
