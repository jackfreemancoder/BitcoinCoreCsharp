using System;

namespace BitcoinCore.BouncyCastle.Utilities
{
	internal interface IMemoable
	{
		/// <summary>
		/// Produce a copy of this object with its configuration and in its current state.
		/// </summary>
		/// <remarks>
		/// The returned object may be used simply to store the state, or may be used as a similar object
		/// starting from the copied state.
		/// </remarks>
		IMemoable Copy();

		/// <summary>
		/// Restore a copied object state into this object.
		/// </summary>
		/// <remarks>
		/// Implementations of this method <em>should</em> try to avoid or minimise memory allocation to perform the reset.
		/// </remarks>
		/// <param name="other">an object originally {@link #copy() copied} from an object of the same type as this instance.</param>
		/// <exception cref="InvalidCastException">if the provided object is not of the correct type.</exception>
		void Reset(IMemoable other);
	}

}

