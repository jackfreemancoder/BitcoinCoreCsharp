﻿#nullable enable
using BitcoinCore.DataEncoders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using BitcoinCore.Protocol;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
#if !HAS_SPAN
using BitcoinCore.BouncyCastle.Math;
#endif
using System.Runtime.InteropServices;
#if !NOSOCKET
using System.Net.Sockets;
using System.Diagnostics.CodeAnalysis;
#endif
#if WINDOWS_UWP
using System.Net.Sockets;
using Windows.Networking;
using Windows.Networking.Connectivity;
#endif

namespace BitcoinCore
{
	public static class Extensions
	{
#if HAS_SPAN
		internal static Crypto.ECDSASignature Sign(this Secp256k1.ECPrivKey key, uint256 h, bool enforceLowR)
		{
			return new Crypto.ECDSASignature(key.Sign(h, enforceLowR, out _));
		}
		internal static Secp256k1.SecpECDSASignature Sign(this Secp256k1.ECPrivKey key, uint256 h, bool enforceLowR, out int recid)
		{
			Span<byte> hash = stackalloc byte[32];
			h.ToBytes(hash);
			byte[]? extra_entropy = null;
			Secp256k1.RFC6979NonceFunction? nonceFunction = null;
			Span<byte> vchSig = stackalloc byte[Secp256k1.SecpECDSASignature.MaxLength];
			Secp256k1.SecpECDSASignature? sig;
			uint counter = 0;
			key.TrySignECDSA(hash, null, out recid, out sig);
			// Grind for low R
			while (sig is not null && sig.r.IsHigh && enforceLowR)
			{
				if (extra_entropy == null || nonceFunction == null)
				{
					extra_entropy = new byte[32];
					nonceFunction = new Secp256k1.RFC6979NonceFunction(extra_entropy);
				}
				Utils.ToBytes(++counter, true, extra_entropy.AsSpan());
				key.TrySignECDSA(hash, nonceFunction, out recid, out sig);
			}
			return sig!;
		}
#endif
		/// <summary>
		/// Deriving an HDKey is normally time consuming, this wrap the IHDKey in a new HD object which can cache derivations
		/// </summary>
		/// <param name="hdkey">The hdKey to wrap</param>
		/// <returns>An hdkey which cache derivations, of the parameter if it is already itself a cache</returns>
		public static IHDKey AsHDKeyCache(this IHDKey hdkey)
		{
			if (hdkey == null)
				throw new ArgumentNullException(nameof(hdkey));
			if (hdkey is HDKeyCache c)
				return c;
			return new HDKeyCache(hdkey);
		}
		/// <summary>
		/// Deriving an IHDScriptPubKey is normally time consuming, this wrap the IHDScriptPubKey in a new IHDScriptPubKey object which can cache derivations
		/// </summary>
		/// <param name="hdScriptPubKey">The hdScriptPubKey to wrap</param>
		/// <returns>An hdkey which cache derivations, of the parameter if it is already itself a cache</returns>
		public static IHDScriptPubKey AsHDKeyCache(this IHDScriptPubKey hdScriptPubKey)
		{
			if (hdScriptPubKey == null)
				throw new ArgumentNullException(nameof(hdScriptPubKey));
			if (hdScriptPubKey is HDScriptPubKeyCache c)
				return c;
			return new HDScriptPubKeyCache(hdScriptPubKey);
		}

		public static IHDScriptPubKey AsHDScriptPubKey(this IHDKey hdKey, ScriptPubKeyType type)
		{
			if (hdKey == null)
				throw new ArgumentNullException(nameof(hdKey));
			return new HDKeyScriptPubKey(hdKey, type);
		}

		public static IHDKey? Derive(this IHDKey hdkey, uint index)
		{
			if (hdkey == null)
				throw new ArgumentNullException(nameof(hdkey));
			return hdkey.Derive(new KeyPath(index));
		}

		/// <summary>
		/// Derive keyPaths as fast as possible using caching and parallelism
		/// </summary>
		/// <param name="hdkey">The hdKey to derive</param>
		/// <param name="keyPaths">keyPaths to derive</param>
		/// <returns>An array of keyPaths.Length size with the derived keys</returns>
		public static IHDKey?[] Derive(this IHDKey hdkey, KeyPath[] keyPaths)
		{
			if (hdkey == null)
				throw new ArgumentNullException(nameof(hdkey));
			if (keyPaths == null)
				throw new ArgumentNullException(nameof(keyPaths));
			var result = new IHDKey?[keyPaths.Length];
			var cache = (HDKeyCache)hdkey.AsHDKeyCache();
#if !NOPARALLEL
			Parallel.For(0, keyPaths.Length, i =>
			{
				result[i] = hdkey.Derive(keyPaths[i]);
			});
#else
			for (int i = 0; i < keyPaths.Length; i++)
			{
				result[i] = hdkey.Derive(keyPaths[i]);
			}
#endif
			return result;
		}
		public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
		{
#if !NO_SOCKETASYNC
			await task.WaitAsync(cancellationToken).ConfigureAwait(false);
#else
			using (var delayCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
			{
				var waiting = Task.Delay(-1, delayCTS.Token);
				var doing = task;
				if (await Task.WhenAny(waiting, doing).ConfigureAwait(false) == waiting)
				{
#pragma warning disable CS4014
					// Need to handle potential exception unhandled later, the original exception is not yet finished
					doing.ContinueWith(_ => _?.Exception?.Handle((e) => true));
#pragma warning restore CS4014
				}
				delayCTS.Cancel();
				cancellationToken.ThrowIfCancellationRequested();
				await doing.ConfigureAwait(false);
			}
#endif
		}

		public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
		{
			using (var delayCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
			{
				var waiting = Task.Delay(-1, delayCTS.Token);
				var doing = task;
				if (await Task.WhenAny(waiting, doing).ConfigureAwait(false) == waiting)
				{
#pragma warning disable CS4014
					// Need to handle potential exception unhandled later, the original exception is not yet finished
					doing.ContinueWith(_ => _?.Exception?.Handle((e) => true));
#pragma warning restore CS4014
				}
				delayCTS.Cancel();
				cancellationToken.ThrowIfCancellationRequested();
				return await doing.ConfigureAwait(false);
			}
		}

		public static Block GetBlock(this IBlockRepository repository, uint256 blockId)
		{
			return repository.GetBlockAsync(blockId, CancellationToken.None).GetAwaiter().GetResult();
		}

		public static T ToNetwork<T>(this T obj, ChainName chainName) where T : IBitcoinString
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));
			if (chainName is null)
				throw new ArgumentNullException(nameof(chainName));
			if (obj.Network.ChainName == chainName)
				return obj;
			return obj.ToNetwork(obj.Network.NetworkSet.GetNetwork(chainName));
		}

		public static T ToNetwork<T>(this T obj, Network network) where T : IBitcoinString
		{
			if (network is null)
				throw new ArgumentNullException(nameof(network));
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));
			if (obj.Network == network)
				return obj;
			if (obj is IBase58Data)
			{
				var b58 = (IBase58Data)obj;
				if (b58.Type != Base58Type.COLORED_ADDRESS)
				{

					byte[] version = network.GetVersionBytes(b58.Type, true)!;
					var enc = network.NetworkStringParser.GetBase58CheckEncoder();
					var inner = enc.DecodeData(b58.ToString()).Skip(version.Length).ToArray();
					var newBase58 = enc.EncodeData(version.Concat(inner).ToArray());
					return Network.Parse<T>(newBase58, network);
				}
				else
				{
					var colored = BitcoinColoredAddress.GetWrappedBase58(obj.ToString(), obj.Network);
					var address = Network.Parse<BitcoinAddress>(colored, obj.Network).ToNetwork(network);
					return (T)(object)address.ToColoredAddress();
				}
			}
			else if (obj is IBech32Data)
			{
				var b32 = (IBech32Data)obj;
				var encoder = b32.Network.GetBech32Encoder(b32.Type, true)!;
				byte wit;
				var data = encoder.Decode(b32.ToString(), out wit);
				encoder = network.GetBech32Encoder(b32.Type, true)!;
				var str = encoder.Encode(wit, data);
				return (T)(object)Network.Parse<T>(str, network);
			}
			else
				throw new NotSupportedException();
		}

		public static byte[] ReadBytes(this Stream stream, int bytesToRead)
		{
			var buffer = new byte[bytesToRead];
			ReadBytes(stream, bytesToRead, buffer);
			return buffer;
		}

		public static int ReadBytes(this Stream stream, int bytesToRead, byte[] buffer)
		{
			int num = 0;
			int num2;
			do
			{
				num += (num2 = stream.Read(buffer, num, bytesToRead - num));
			} while (num2 > 0 && num < bytesToRead);
			return num;
		}

		public static async Task<byte[]> ReadBytesAsync(this Stream stream, int bytesToRead)
		{
			var buffer = new byte[bytesToRead];
			int num = 0;
			int num2;
			do
			{
				num += (num2 = await stream.ReadAsync(buffer, num, bytesToRead - num).ConfigureAwait(false));
			} while (num2 > 0 && num < bytesToRead);
			return buffer;
		}

		public static int ReadBytes(this Stream stream, int count, out byte[] result)
		{
			result = new byte[count];
			return stream.Read(result, 0, count);
		}
		public static IEnumerable<T?> Resize<T>(this List<T?> list, int count)
		{
			if (list.Count == count)
				return new T[0];

			var removed = new List<T?>();

			for (int i = list.Count - 1; i + 1 > count; i--)
			{
				removed.Add(list[i]);
				list.RemoveAt(i);
			}

			while (list.Count < count)
			{
				list.Add(default(T));
			}
			return removed;
		}
		public static IEnumerable<List<T>> Partition<T>(this IEnumerable<T> source, int max)
		{
			return Partition(source, () => max);
		}
		public static IEnumerable<List<T>> Partition<T>(this IEnumerable<T> source, Func<int> max)
		{
			var partitionSize = max();
			List<T> toReturn = new List<T>(partitionSize);
			foreach (var item in source)
			{
				toReturn.Add(item);
				if (toReturn.Count == partitionSize)
				{
					yield return toReturn;
					partitionSize = max();
					toReturn = new List<T>(partitionSize);
				}
			}
			if (toReturn.Any())
			{
				yield return toReturn;
			}
		}

#if !NETSTANDARD1X
		public static int ReadEx(this Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellation = default(CancellationToken))
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream));
			if (buffer == null)
				throw new ArgumentNullException(nameof(buffer));
			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException("offset");
			if (count <= 0 || count > buffer.Length)
				throw new ArgumentOutOfRangeException("count"); //Disallow 0 as a debugging aid.
			if (offset > buffer.Length - count)
				throw new ArgumentOutOfRangeException("count");

			int totalReadCount = 0;

			while (totalReadCount < count)
			{
				cancellation.ThrowIfCancellationRequested();

				int currentReadCount;

				//Big performance problem with BeginRead for other stream types than NetworkStream.
				//Only take the slow path if cancellation is possible.
				if (stream is NetworkStream && cancellation.CanBeCanceled)
				{
					var ar = stream.BeginRead(buffer, offset + totalReadCount, count - totalReadCount, null, null);
					if (!ar.CompletedSynchronously)
					{
						WaitHandle.WaitAny(new WaitHandle[] { ar.AsyncWaitHandle, cancellation.WaitHandle }, -1);
					}

					//EndRead might block, so we need to test cancellation before calling it.
					//This also is a bug because calling EndRead after BeginRead is contractually required.
					//A potential fix is to use the ReadAsync API. Another fix is to register a callback with BeginRead that calls EndRead in all cases.
					cancellation.ThrowIfCancellationRequested();

					currentReadCount = stream.EndRead(ar);
				}
				else
				{
					//IO interruption not supported in this path.
					currentReadCount = stream.Read(buffer, offset + totalReadCount, count - totalReadCount);
				}

				if (currentReadCount == 0)
					return 0;

				totalReadCount += currentReadCount;
			}

			return totalReadCount;
		}
#else

		public static int ReadEx(this Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellation = default(CancellationToken))
		{
			if(stream == null) throw new ArgumentNullException(nameof(stream));
			if(buffer == null) throw new ArgumentNullException(nameof(buffer));
			if(offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException("offset");
			if(count <= 0 || count > buffer.Length) throw new ArgumentOutOfRangeException("count"); //Disallow 0 as a debugging aid.
			if(offset > buffer.Length - count) throw new ArgumentOutOfRangeException("count");

			//IO interruption not supported on these platforms.

			int totalReadCount = 0;
#if !NOSOCKET
			var interruptable = stream is NetworkStream && cancellation.CanBeCanceled;
#endif
			while(totalReadCount < count)
			{
				cancellation.ThrowIfCancellationRequested();
				int currentReadCount = 0;
#if !NOSOCKET
				if(interruptable)
				{
					currentReadCount = stream.ReadAsync(buffer, offset + totalReadCount, count - totalReadCount, cancellation).GetAwaiter().GetResult();
				}
				else
#endif
				{
					currentReadCount = stream.Read(buffer, offset + totalReadCount, count - totalReadCount);
				}
				if(currentReadCount == 0)
					return 0;
				totalReadCount += currentReadCount;
			}

			return totalReadCount;
		}
#endif

#if HAS_SPAN
		public static int ReadEx(this Stream stream, Span<byte> buffer, CancellationToken cancellation = default(CancellationToken))
		{
			if(stream == null)
				throw new ArgumentNullException(nameof(stream));
			int totalReadCount = 0;
			while(!buffer.IsEmpty)
			{
				cancellation.ThrowIfCancellationRequested();
				int currentReadCount = stream.Read(buffer);
				if(currentReadCount == 0)
					return 0;
				buffer = buffer.Slice(currentReadCount);
				totalReadCount += currentReadCount;
			}

			return totalReadCount;
		}
#endif
		public static void AddOrReplace<TKey, TValue>(this IDictionary<TKey, TValue> dico, TKey key, TValue value)
		{
			if (dico.ContainsKey(key))
				dico[key] = value;
			else
				dico.Add(key, value);
		}

		public static TValue? TryGet<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
		{
			TValue? value;
			dictionary.TryGetValue(key, out value);
			return value;
		}

		public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
		{
			if (!dictionary.ContainsKey(key))
			{
				dictionary.Add(key, value);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Converts a given DateTime into a Unix timestamp
		/// </summary>
		/// <param name="value">Any DateTime</param>
		/// <returns>The given DateTime in Unix timestamp format</returns>
		public static int ToUnixTimestamp(this DateTime value)
		{
			return (int)Math.Truncate((value.ToUniversalTime().Subtract(new DateTime(1970, 1, 1))).TotalSeconds);
		}

		/// <summary>
		/// Gets a Unix timestamp representing the current moment
		/// </summary>
		/// <param name="ignored">Parameter ignored</param>
		/// <returns>Now expressed as a Unix timestamp</returns>
		public static int UnixTimestamp(this DateTime ignored)
		{
			return (int)Math.Truncate((DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds);
		}
	}

	internal static class ByteArrayExtensions
	{
		internal static bool StartWith(this byte[] data, byte[] versionBytes)
		{
			if (data.Length < versionBytes.Length)
				return false;
			for (int i = 0; i < versionBytes.Length; i++)
			{
				if (data[i] != versionBytes[i])
					return false;
			}
			return true;
		}
		internal static byte[] SafeSubarray(this byte[] array, int offset, int count)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (offset < 0 || offset > array.Length)
				throw new ArgumentOutOfRangeException("offset");
			if (count < 0 || offset + count > array.Length)
				throw new ArgumentOutOfRangeException("count");
			if (offset == 0 && array.Length == count)
				return array;
			var data = new byte[count];
			Buffer.BlockCopy(array, offset, data, 0, count);
			return data;
		}

		internal static byte[] SafeSubarray(this byte[] array, int offset)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (offset < 0 || offset > array.Length)
				throw new ArgumentOutOfRangeException("offset");

			var count = array.Length - offset;
			var data = new byte[count];
			Buffer.BlockCopy(array, offset, data, 0, count);
			return data;
		}

		internal static byte[] Concat(this byte[] arr, params byte[][] arrs)
		{
			var len = arr.Length + arrs.Sum(a => a.Length);
			var ret = new byte[len];
			Buffer.BlockCopy(arr, 0, ret, 0, arr.Length);
			var pos = arr.Length;
			foreach (var a in arrs)
			{
				Buffer.BlockCopy(a, 0, ret, pos, a.Length);
				pos += a.Length;
			}
			return ret;
		}

	}

	public class Utils
	{
		internal static void SafeSet(ManualResetEvent ar)
		{
			try
			{
#if !NETSTANDARD1X
				if (!ar.SafeWaitHandle.IsClosed && !ar.SafeWaitHandle.IsInvalid)
					ar.Set();
#else
				ar.Set();
#endif
			}
			catch { }
		}
		public static bool ArrayEqual(byte[] a, byte[] b)
		{
			if (a == null && b == null)
				return true;
			if (a == null)
				return false;
			if (b == null)
				return false;
			return ArrayEqual(a, 0, b, 0, Math.Max(a.Length, b.Length));
		}
		public static bool ArrayEqual(byte[] a, int startA, byte[] b, int startB, int length)
		{
			if (a == null && b == null)
				return true;
			if (a == null)
				return false;
			if (b == null)
				return false;
			var alen = a.Length - startA;
			var blen = b.Length - startB;

			if (alen < length || blen < length)
				return false;

			for (int ai = startA, bi = startB; ai < startA + length; ai++, bi++)
			{
				if (a[ai] != b[bi])
					return false;
			}
			return true;
		}

		private static void Write(MemoryStream ms, byte[] bytes)
		{
			ms.Write(bytes, 0, bytes.Length);
		}
#if !HAS_SPAN
		internal static byte[] BigIntegerToBytes(BigInteger b, int numBytes)
		{
			byte[] bytes = new byte[numBytes];
			byte[] biBytes = b.ToByteArray();
			int start = (biBytes.Length == numBytes + 1) ? 1 : 0;
			int length = Math.Min(biBytes.Length, numBytes);
			Array.Copy(biBytes, start, bytes, numBytes - length, length);
			return bytes;
		}
		internal static byte[] BigIntegerToBytes(BigInteger num)
		{
			if (num.Equals(BigInteger.Zero))
				//Positive 0 is represented by a null-length vector
				return new byte[0];

			bool isPositive = true;
			if (num.CompareTo(BigInteger.Zero) < 0)
			{
				isPositive = false;
				num = num.Multiply(BigInteger.ValueOf(-1));
			}
			var array = num.ToByteArray();
			Array.Reverse(array);
			if (!isPositive)
				array[array.Length - 1] |= 0x80;
			return array;
		}

		internal static BigInteger BytesToBigInteger(byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));
			if (data.Length == 0)
				return BigInteger.Zero;
			data = data.ToArray();
			var positive = (data[data.Length - 1] & 0x80) == 0;
			if (!positive)
			{
				data[data.Length - 1] &= unchecked((byte)~0x80);
				Array.Reverse(data);
				return new BigInteger(1, data).Negate();
			}
			return new BigInteger(1, data);
		}
#endif
		static DateTimeOffset unixRef = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

		public static uint DateTimeToUnixTime(DateTimeOffset dt)
		{
			return (uint)DateTimeToUnixTimeLong(dt);
		}

		internal static ulong DateTimeToUnixTimeLong(DateTimeOffset dt)
		{
			dt = dt.ToUniversalTime();
			if (dt < unixRef)
				throw new ArgumentOutOfRangeException("The supplied datetime can't be expressed in unix timestamp");
			var result = (dt - unixRef).TotalSeconds;
			if (result > UInt32.MaxValue)
				throw new ArgumentOutOfRangeException("The supplied datetime can't be expressed in unix timestamp");
			return (ulong)result;
		}

		public static DateTimeOffset UnixTimeToDateTime(uint timestamp)
		{
			var span = TimeSpan.FromSeconds(timestamp);
			return unixRef + span;
		}
		public static DateTimeOffset UnixTimeToDateTime(ulong timestamp)
		{
			var span = TimeSpan.FromSeconds(timestamp);
			return unixRef + span;
		}
		public static DateTimeOffset UnixTimeToDateTime(long timestamp)
		{
			var span = TimeSpan.FromSeconds(timestamp);
			return unixRef + span;
		}



		public static string ExceptionToString(Exception exception)
		{
			Exception? ex = exception;
			StringBuilder stringBuilder = new StringBuilder(128);
			while (ex != null)
			{
				stringBuilder.Append(ex.GetType().Name);
				stringBuilder.Append(": ");
				stringBuilder.Append(ex.Message);
				stringBuilder.AppendLine(ex.StackTrace);
				ex = ex.InnerException;
				if (ex != null)
				{
					stringBuilder.Append(" ---> ");
				}
			}
			return stringBuilder.ToString();
		}

		public static void Shuffle<T>(T[] arr, Random? rand)
		{
			rand = rand ?? new Random();
			for (int i = 0; i < arr.Length; i++)
			{
				var fromIndex = rand.Next(arr.Length);
				var from = arr[fromIndex];

				var toIndex = rand.Next(arr.Length);
				var to = arr[toIndex];

				arr[toIndex] = from;
				arr[fromIndex] = to;
			}
		}
		public static void Shuffle<T>(List<T> arr, int start, Random rand)
		{
			rand = rand ?? new Random();
			for (int i = start; i < arr.Count; i++)
			{
				var fromIndex = rand.Next(start, arr.Count);
				var from = arr[fromIndex];

				var toIndex = rand.Next(start, arr.Count);
				var to = arr[toIndex];

				arr[toIndex] = from;
				arr[fromIndex] = to;
			}
		}
		public static void Shuffle<T>(List<T> arr, Random rand)
		{
			Shuffle(arr, 0, rand);
		}
		public static void Shuffle<T>(T[] arr, int seed)
		{
			Random rand = new Random(seed);
			Shuffle(arr, rand);
		}

		public static void Shuffle<T>(T[] arr)
		{
			Shuffle(arr, null);
		}


#if !NOSOCKET
		internal static void SafeCloseSocket(System.Net.Sockets.Socket socket)
		{
			try
			{
				socket.Shutdown(SocketShutdown.Both);
			}
			catch
			{
			}
			try
			{
				socket.Dispose();
			}
			catch
			{
			}
		}

		public static System.Net.IPEndPoint EnsureIPv6(System.Net.IPEndPoint endpoint)
		{
			if (endpoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
				return endpoint;
			return new IPEndPoint(endpoint.Address.MapToIPv6(), endpoint.Port);
		}
#endif
		public static byte[] ToBytes(uint value, bool littleEndian)
		{
#if HAS_SPAN
			if (littleEndian && BitConverter.IsLittleEndian)
			{
				var result = new byte[4];
				MemoryMarshal.Cast<byte, uint>(result)[0] = value;
				return result;
			}
#endif
			if (littleEndian)
			{
				return new byte[]
				{
					(byte)value,
					(byte)(value >> 8),
					(byte)(value >> 16),
					(byte)(value >> 24),
				};
			}
			else
			{
				return new byte[]
				{
					(byte)(value >> 24),
					(byte)(value >> 16),
					(byte)(value >> 8),
					(byte)value,
				};
			}
		}

		public static byte[] ToBytes(ulong value, bool littleEndian)
		{
#if HAS_SPAN
			if (littleEndian && BitConverter.IsLittleEndian)
			{
				var result = new byte[8];
				MemoryMarshal.Cast<byte, ulong>(result)[0] = value;
				return result;
			}
#endif
			if (littleEndian)
			{
				return new byte[]
				{
					(byte)value,
					(byte)(value >> 8),
					(byte)(value >> 16),
					(byte)(value >> 24),
					(byte)(value >> 32),
					(byte)(value >> 40),
					(byte)(value >> 48),
					(byte)(value >> 56),
				};
			}
			else
			{
				return new byte[]
				{
					(byte)(value >> 56),
					(byte)(value >> 48),
					(byte)(value >> 40),
					(byte)(value >> 32),
					(byte)(value >> 24),
					(byte)(value >> 16),
					(byte)(value >> 8),
					(byte)value,
				};
			}
		}

#if HAS_SPAN
		public static void ToBytes(uint value, bool littleEndian, Span<byte> output)
		{
			if (littleEndian && BitConverter.IsLittleEndian)
			{
				MemoryMarshal.Cast<byte, uint>(output)[0] = value;
				return;
			}

			if (littleEndian)
			{
				output[0] = (byte)value;
				output[1] = (byte)(value >> 8);
				output[2] = (byte)(value >> 16);
				output[3] = (byte)(value >> 24);
			}
			else
			{
				output[0] = (byte)(value >> 24);
				output[1] = (byte)(value >> 16);
				output[2] = (byte)(value >> 8);
				output[3] = (byte)value;
			}
		}
		public static void ToBytes(ulong value, bool littleEndian, Span<byte> output)
		{
			if (littleEndian && BitConverter.IsLittleEndian)
			{
				MemoryMarshal.Cast<byte, ulong>(output)[0] = value;
				return;
			}

			if (littleEndian)
			{
				output[0] = (byte)value;
				output[1] = (byte)(value >> 8);
				output[2] = (byte)(value >> 16);
				output[3] = (byte)(value >> 24);
				output[4] = (byte)(value >> 32);
				output[5] = (byte)(value >> 40);
				output[6] = (byte)(value >> 48);
				output[7] = (byte)(value >> 56);
			}
			else
			{
				output[0] = (byte)(value >> 56);
				output[1] = (byte)(value >> 48);
				output[2] = (byte)(value >> 40);
				output[3] = (byte)(value >> 32);
				output[4] = (byte)(value >> 24);
				output[5] = (byte)(value >> 16);
				output[6] = (byte)(value >> 8);
				output[7] = (byte)value;
			}
		}
#endif

		public static uint ToUInt32(byte[] value, int index, bool littleEndian)
		{
#if HAS_SPAN
			if (littleEndian && BitConverter.IsLittleEndian)
			{
				return MemoryMarshal.Cast<byte, uint>(value.AsSpan().Slice(index))[0];
			}
#endif
			if (littleEndian)
			{
				return value[index]
					   + ((uint)value[index + 1] << 8)
					   + ((uint)value[index + 2] << 16)
					   + ((uint)value[index + 3] << 24);
			}
			else
			{
				return value[index + 3]
					   + ((uint)value[index + 2] << 8)
					   + ((uint)value[index + 1] << 16)
					   + ((uint)value[index + 0] << 24);
			}
		}
#if HAS_SPAN
		public static uint ToUInt32(ReadOnlySpan<byte> value, bool littleEndian)
		{
			if (littleEndian && BitConverter.IsLittleEndian)
			{
				return MemoryMarshal.Cast<byte, uint>(value)[0];
			}
			if (littleEndian)
			{
				return value[0]
					   + ((uint)value[1] << 8)
					   + ((uint)value[2] << 16)
					   + ((uint)value[3] << 24);
			}
			else
			{
				return value[3]
					   + ((uint)value[2] << 8)
					   + ((uint)value[1] << 16)
					   + ((uint)value[0] << 24);
			}
		}
#endif


		public static int ToInt32(byte[] value, int index, bool littleEndian)
		{
			return unchecked((int)ToUInt32(value, index, littleEndian));
		}

		public static uint ToUInt32(byte[] value, bool littleEndian)
		{
			return ToUInt32(value, 0, littleEndian);
		}

		public static ulong ToUInt64(byte[] value, int offset, bool littleEndian)
		{
#if HAS_SPAN
			if (littleEndian && BitConverter.IsLittleEndian)
			{
				return MemoryMarshal.Cast<byte, ulong>(value.AsSpan().Slice(offset))[0];
			}
#endif
			if (littleEndian)
			{
				return value[offset + 0]
					   + ((ulong)value[offset + 1] << 8)
					   + ((ulong)value[offset + 2] << 16)
					   + ((ulong)value[offset + 3] << 24)
					   + ((ulong)value[offset + 4] << 32)
					   + ((ulong)value[offset + 5] << 40)
					   + ((ulong)value[offset + 6] << 48)
					   + ((ulong)value[offset + 7] << 56);
			}
			else
			{
				return value[offset + 7]
					+ ((ulong)value[offset + 6] << 8)
					+ ((ulong)value[offset + 5] << 16)
					+ ((ulong)value[offset + 4] << 24)
					+ ((ulong)value[offset + 3] << 32)
					   + ((ulong)value[offset + 2] << 40)
					   + ((ulong)value[offset + 1] << 48)
					   + ((ulong)value[offset + 0] << 56);
			}
		}
#if HAS_SPAN
		public static ulong ToUInt64(ReadOnlySpan<byte> value, bool littleEndian)
		{
			if (littleEndian && BitConverter.IsLittleEndian)
			{
				return MemoryMarshal.Cast<byte, ulong>(value)[0];
			}
			if (littleEndian)
			{
				return value[0]
					   + ((ulong)value[1] << 8)
					   + ((ulong)value[2] << 16)
					   + ((ulong)value[3] << 24)
					   + ((ulong)value[4] << 32)
					   + ((ulong)value[5] << 40)
					   + ((ulong)value[6] << 48)
					   + ((ulong)value[7] << 56);
			}
			else
			{
				return value[7]
					+ ((ulong)value[6] << 8)
					+ ((ulong)value[5] << 16)
					+ ((ulong)value[4] << 24)
					+ ((ulong)value[3] << 32)
					   + ((ulong)value[2] << 40)
					   + ((ulong)value[1] << 48)
					   + ((ulong)value[0] << 56);
			}
		}
#endif

		public static ulong ToUInt64(byte[] value, bool littleEndian)
		{
			return ToUInt64(value, 0, littleEndian);
		}


#if !NOSOCKET

		public static bool TryParseEndpoint(string hostPort, int defaultPort, [MaybeNullWhen(false)] out EndPoint endpoint)
		{
			if (hostPort == null)
				throw new ArgumentNullException(nameof(hostPort));
			if (defaultPort < 0 || defaultPort > ushort.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(defaultPort));
			hostPort = hostPort.Trim();
			endpoint = null;
			ushort port = (ushort)defaultPort;
			string host = hostPort;
			var index = hostPort.LastIndexOf(':');
			if (index != -1)
			{
				var index2 = hostPort.IndexOf(':');
				if (index2 == index || hostPort.IndexOf(']') != -1)
				{
					var portStr = hostPort.Substring(index + 1);
					if (ushort.TryParse(portStr, out port))
					{
						host = hostPort.Substring(0, index);
					}
					else
					{
						port = (ushort)defaultPort;
					}
				}
				else // At least two ':', this should be considered IPv6 without port
				{
					port = (ushort)defaultPort;
				}
			}
			if (IPAddress.TryParse(host, out var address))
			{
				endpoint = new IPEndPoint(address, port);
			}
			else
			{
				if (Uri.CheckHostName(host) != UriHostNameType.Dns ||
					// An host name with a length higher than 255 can't be resolved by DNS
					host.Length > 255)
					return false;
				endpoint = new DnsEndPoint(host, port);
			}
			return true;
		}

		public static EndPoint ParseEndpoint(string hostPort, int defaultPort)
		{
			if (!TryParseEndpoint(hostPort, defaultPort, out var endpoint))
				throw new FormatException("Invalid IP or DNS endpoint");
			return endpoint;
		}

#endif
		public static int GetHashCode(byte[] array)
		{
			return BitcoinCore.BouncyCastle.Utilities.Arrays.GetHashCode(array);
		}
	}
}
