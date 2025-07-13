using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinCore.Protocol
{

	public class VerAckPayload : Payload, IBitcoinSerializable
	{
		public override string Command => "verack";
		#region IBitcoinSerializable Members

		public override void ReadWriteCore(BitcoinStream stream)
		{
		}

		#endregion
	}
}
