﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinCore.Protocol
{
	/// <summary>
	/// A merkle block received after being asked with a getdata message
	/// </summary>

	public class MerkleBlockPayload : BitcoinSerializablePayload<MerkleBlock>
	{
		public override string Command => "merkleblock";
		public MerkleBlockPayload()
		{

		}
		public MerkleBlockPayload(MerkleBlock block)
			: base(block)
		{

		}
	}
}
