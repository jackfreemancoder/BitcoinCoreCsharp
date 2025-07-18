﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinCore.Protocol
{
	/// <summary>
	/// Represents a transaction being sent on the network, is sent after being requested by a getdata (of Transaction or MerkleBlock) message.
	/// </summary>

	public class TxPayload : Payload
	{
		public override string Command => "tx";
		public TxPayload()
		{

		}
		public TxPayload(Transaction transaction)
		{
			_Object = transaction;
		}

		Transaction _Object;
		public Transaction Object
		{
			get
			{
				return _Object;
			}
			set
			{
				_Object = value;
			}
		}

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite(ref _Object);
		}
	}
}
