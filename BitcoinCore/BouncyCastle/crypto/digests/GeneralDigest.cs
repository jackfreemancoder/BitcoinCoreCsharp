using System;

using BitcoinCore.BouncyCastle.Utilities;

namespace BitcoinCore.BouncyCastle.Crypto.Digests
{
	/**
    * base implementation of MD4 family style digest as outlined in
    * "Handbook of Applied Cryptography", pages 344 - 347.
    */
	internal abstract class GeneralDigest
#if !NO_BC
		: IDigest, IMemoable
#endif
	{
		private const int BYTE_LENGTH = 64;

		private byte[] xBuf;
		private int xBufOff;

		private long byteCount;

		internal GeneralDigest()
		{
			xBuf = new byte[4];
		}

		internal GeneralDigest(GeneralDigest t)
		{
			xBuf = new byte[t.xBuf.Length];
			CopyIn(t);
		}

		protected void CopyIn(GeneralDigest t)
		{
			Array.Copy(t.xBuf, 0, xBuf, 0, t.xBuf.Length);

			xBufOff = t.xBufOff;
			byteCount = t.byteCount;
		}

		public void Update(byte input)
		{
			xBuf[xBufOff++] = input;

			if (xBufOff == xBuf.Length)
			{
				ProcessWord(xBuf, 0);
				xBufOff = 0;
			}

			byteCount++;
		}

		public void BlockUpdate(
			byte[] input,
			int inOff,
			int length)
		{
			length = System.Math.Max(0, length);

			//
			// fill the current word
			//
			int i = 0;
			if (xBufOff != 0)
			{
				while (i < length)
				{
					xBuf[xBufOff++] = input[inOff + i++];
					if (xBufOff == 4)
					{
						ProcessWord(xBuf, 0);
						xBufOff = 0;
						break;
					}
				}
			}

			//
			// process whole words.
			//
			int limit = ((length - i) & ~3) + i;
			for (; i < limit; i += 4)
			{
				ProcessWord(input, inOff + i);
			}

			//
			// load in the remainder.
			//
			while (i < length)
			{
				xBuf[xBufOff++] = input[inOff + i++];
			}

			byteCount += length;
		}

		public void Finish()
		{
			long bitLength = (byteCount << 3);

			//
			// add the pad bytes.
			//
			Update((byte)128);

			while (xBufOff != 0)
				Update((byte)0);
			ProcessLength(bitLength);
			ProcessBlock();
		}

		public virtual void Reset()
		{
			byteCount = 0;
			xBufOff = 0;
			Array.Clear(xBuf, 0, xBuf.Length);
		}

		public int GetByteLength()
		{
			return BYTE_LENGTH;
		}

		internal abstract void ProcessWord(byte[] input, int inOff);
		internal abstract void ProcessLength(long bitLength);
		internal abstract void ProcessBlock();
		public abstract string AlgorithmName
		{
			get;
		}
		public abstract int GetDigestSize();
		public abstract int DoFinal(byte[] output, int outOff);
#if !NO_BC
		public abstract IMemoable Copy();
		public abstract void Reset(IMemoable t);
#endif
	}
}
