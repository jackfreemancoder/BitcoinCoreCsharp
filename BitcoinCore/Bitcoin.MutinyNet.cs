﻿using System;
using System.IO;
using BitcoinCore.Crypto;

namespace BitcoinCore
{
	public partial class Bitcoin
	{
		static readonly ChainName MutinynetName = new("Mutinynet");

		public Network Mutinynet => _Networks[MutinynetName];

		private Network CreateMutinyNet()
		{
			NetworkBuilder builder = new NetworkBuilder();
			builder.SetChainName(MutinynetName);
			builder.SetNetworkSet(this);
			builder.SetConsensus(new Consensus()
				{
					SubsidyHalvingInterval = 210000,
					MajorityEnforceBlockUpgrade = 750,
					MajorityRejectBlockOutdated = 950,
					MajorityWindow = 1000,
					BIP34Hash = new uint256(),
					PowLimit = new Target(
						new uint256("00000377ae000000000000000000000000000000000000000000000000000000")),
					PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60),
					PowTargetSpacing = TimeSpan.FromSeconds(30),
					PowAllowMinDifficultyBlocks = false,
					PowNoRetargeting = false,
					RuleChangeActivationThreshold = 1916,
					MinerConfirmationWindow = 2016,
					CoinbaseMaturity = 100,
					SupportSegwit = true,
					SupportTaproot = true
				})
				.SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] {111})
				.SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] {196})
				.SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] {239})
				.SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] {0x04, 0x35, 0x87, 0xCF})
				.SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] {0x04, 0x35, 0x83, 0x94})
				.SetBech32(Bech32Type.WITNESS_PUBKEY_ADDRESS, "tb")
				.SetBech32(Bech32Type.WITNESS_SCRIPT_ADDRESS, "tb")
				.SetBech32(Bech32Type.TAPROOT_ADDRESS, "tb")
				.SetMagic(GetMutinynetMagic())
				.SetPort(38333)
				.SetRPCPort(38332)
				.SetName("mutinynet")
				.AddAlias("bitcoin-mutinynet")
				.AddAlias("btc-mutinynet")
#if !NOSOCKET
				.AddSeeds(new[]
				{
					new Protocol.NetworkAddress(System.Net.IPAddress.Parse("45.79.52.207"), 38333)
				})
#endif
				.SetGenesis(
					"0100000000000000000000000000000000000000000000000000000000000000000000003ba3edfd7a7b12b27ac72c3e67768f617fc81bc3888a51323a9fb8aa4b1e5e4a008f4d5fae77031e8ad222030101000000010000000000000000000000000000000000000000000000000000000000000000ffffffff4d04ffff001d0104455468652054696d65732030332f4a616e2f32303039204368616e63656c6c6f72206f6e206272696e6b206f66207365636f6e64206261696c6f757420666f722062616e6b73ffffffff0100f2052a01000000434104678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5fac00000000");

			var network = builder.BuildAndRegister();
#if !NOFILEIO
			var data = Network.GetDefaultDataFolder("bitcoin");
			if (data != null)
			{
				var signetCookie = Path.Combine(data, "signet", ".cookie");
				RPC.RPCClient.RegisterDefaultCookiePath(network, signetCookie);
			}
#endif
			_Networks.TryAdd(MutinynetName, network);
			return network;
		}


		private static uint GetMutinynetMagic()
		{
			var challengeBytes = DataEncoders.Encoders.Hex.DecodeData(
				"512102f7561d208dd9ae99bf497273e16f389bdbd6c4742ddb8e6b216e64fa2928ad8f51ae");
			var challenge = new Script(challengeBytes);
			MemoryStream ms = new MemoryStream();
			BitcoinStream bitcoinStream = new BitcoinStream(ms, true);
			bitcoinStream.ReadWrite(challenge);
			var h = Hashes.DoubleSHA256RawBytes(ms.ToArray(), 0, (int) ms.Length);
			return Utils.ToUInt32(h, true);
		}

	}
}
