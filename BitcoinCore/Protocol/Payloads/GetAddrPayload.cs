﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinCore.Protocol
{
	/// <summary>
	/// Ask for known peer addresses in the network
	/// </summary>

	public class GetAddrPayload : Payload
	{
		public override string Command => "getaddr";
	}
}
