﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinCore.Protocol
{

	public class HaveWitnessPayload : Payload
	{
		public override string Command => "havewitness";
		public HaveWitnessPayload()
		{

		}
	}
}
