using System;

#if !(USEBC || WINDOWS_UWP)
using System.Security.Cryptography;
#else
using BitcoinCore.BouncyCastle.Asn1.X9;
using BitcoinCore.BouncyCastle.Crypto.Parameters;
using BitcoinCore.BouncyCastle.Crypto.Signers;
using BitcoinCore.BouncyCastle.Math;
using BitcoinCore.BouncyCastle.Math.EC;
using BitcoinCore.BouncyCastle.Math.EC.Custom.Sec;
using BitcoinCore.BouncyCastle.Crypto.Paddings;
using BitcoinCore.BouncyCastle.Crypto.Engines;
using BitcoinCore.BouncyCastle.Crypto.Modes;
#endif

namespace BitcoinCore.Crypto
{
	internal class AesWrapper
	{
#if (USEBC || WINDOWS_UWP)
		private PaddedBufferedBlockCipher _inner;
		private AesWrapper(PaddedBufferedBlockCipher aes)
		{
			_inner = aes;
		}

		internal static AesWrapper Create()
		{
			CbcBlockCipher blockCipher = new CbcBlockCipher(new AesFastEngine()); //CBC
			var aes = new PaddedBufferedBlockCipher(blockCipher);
			return new AesWrapper(aes);
		}

		public byte[] Process(byte[] inputBuffer, int inputOffset, int inputCount)
		{
			var outputBuffer = new byte[_inner.GetOutputSize(inputCount)];
			var count = _inner.ProcessBytes(inputBuffer, inputOffset, inputCount, outputBuffer, 0);
			count += _inner.DoFinal(outputBuffer, count);
			return outputBuffer.SafeSubarray(0, count);
		}

		internal void Initialize(byte[] key, byte[] iv, bool forEncryption)
		{
			var keyParamWithIV = new ParametersWithIV(new KeyParameter(key), iv, 0, 16);			
			_inner.Init(forEncryption, keyParamWithIV);
		}
#else

		private Aes _inner;
		private ICryptoTransform _transformer;
		private AesWrapper(Aes aes)
		{
			_inner = aes;
		}

		internal static AesWrapper Create()
		{
			var aes = Aes.Create();
			return new AesWrapper(aes);
		}

		public byte[] Process(byte[] inputBuffer, int inputOffset, int inputCount)
		{
			return _transformer.TransformFinalBlock(inputBuffer, inputOffset, inputCount);
		}

		internal void Initialize(byte[] key, byte[] iv, bool forEncryption)
		{
			if (_transformer != null)
				return;
			_inner.IV = iv;
			_inner.KeySize = key.Length * 8;
			_inner.Key = key;
			_transformer = forEncryption ? _inner.CreateEncryptor() : _inner.CreateDecryptor();
		}
#endif
	}

	internal class AesBuilder
	{
		private byte[] _key;
		private bool? _forEncryption;

		private byte[] _iv = new byte[16];


		public AesBuilder SetKey(byte[] key)
		{
			_key = key;
			return this;
		}

		public AesBuilder IsUsedForEncryption(bool forEncryption)
		{
			_forEncryption = forEncryption;
			return this;
		}

		public AesBuilder SetIv(byte[] iv)
		{
			_iv = iv;
			return this;
		}

		public AesWrapper Build()
		{
			var aes = AesWrapper.Create();
			var encrypt = !_forEncryption.HasValue || _forEncryption.Value;
			aes.Initialize(_key, _iv, encrypt);
			return aes;
		}
	}
}