#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace BitcoinCore
{
	public interface IHDKey
	{
		IHDKey? Derive(KeyPath keyPath);
		PubKey GetPublicKey();
	}
}
