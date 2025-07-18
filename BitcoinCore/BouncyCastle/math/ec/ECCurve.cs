using System;
using System.Collections;

using BitcoinCore.BouncyCastle.Math.EC.Abc;
using BitcoinCore.BouncyCastle.Math.EC.Endo;
using BitcoinCore.BouncyCastle.Math.EC.Multiplier;
using BitcoinCore.BouncyCastle.Math.Field;
using BitcoinCore.BouncyCastle.Utilities;

namespace BitcoinCore.BouncyCastle.Math.EC
{
	/// <remarks>Base class for an elliptic curve.</remarks>
	internal abstract class ECCurve
	{
		public const int COORD_AFFINE = 0;
		public const int COORD_HOMOGENEOUS = 1;
		public const int COORD_JACOBIAN = 2;
		public const int COORD_JACOBIAN_CHUDNOVSKY = 3;
		public const int COORD_JACOBIAN_MODIFIED = 4;
		public const int COORD_LAMBDA_AFFINE = 5;
		public const int COORD_LAMBDA_PROJECTIVE = 6;
		public const int COORD_SKEWED = 7;

		public static int[] GetAllCoordinateSystems()
		{
			return new int[]{ COORD_AFFINE, COORD_HOMOGENEOUS, COORD_JACOBIAN, COORD_JACOBIAN_CHUDNOVSKY,
				COORD_JACOBIAN_MODIFIED, COORD_LAMBDA_AFFINE, COORD_LAMBDA_PROJECTIVE, COORD_SKEWED };
		}

		public class Config
		{
			protected ECCurve outer;
			protected int coord;
			protected ECEndomorphism endomorphism;
			protected ECMultiplier multiplier;

			internal Config(ECCurve outer, int coord, ECEndomorphism endomorphism, ECMultiplier multiplier)
			{
				this.outer = outer;
				this.coord = coord;
				this.endomorphism = endomorphism;
				this.multiplier = multiplier;
			}

			public Config SetCoordinateSystem(int coord)
			{
				this.coord = coord;
				return this;
			}

			public Config SetEndomorphism(ECEndomorphism endomorphism)
			{
				this.endomorphism = endomorphism;
				return this;
			}

			public Config SetMultiplier(ECMultiplier multiplier)
			{
				this.multiplier = multiplier;
				return this;
			}

			public ECCurve Create()
			{
				if (!outer.SupportsCoordinateSystem(coord))
				{
					throw new InvalidOperationException("unsupported coordinate system");
				}

				ECCurve c = outer.CloneCurve();
				if (c == outer)
				{
					throw new InvalidOperationException("implementation returned current curve");
				}

				c.m_coord = coord;
				c.m_endomorphism = endomorphism;
				c.m_multiplier = multiplier;

				return c;
			}
		}

		protected readonly IFiniteField m_field;
		protected ECFieldElement m_a, m_b;
		protected BigInteger m_order, m_cofactor;

		protected int m_coord = COORD_AFFINE;
		protected ECEndomorphism m_endomorphism = null;
		protected ECMultiplier m_multiplier = null;

		protected ECCurve(IFiniteField field)
		{
			this.m_field = field;
		}

		public abstract int FieldSize
		{
			get;
		}
		public abstract ECFieldElement FromBigInteger(BigInteger x);
		public abstract bool IsValidFieldElement(BigInteger x);

		public virtual Config Configure()
		{
			return new Config(this, this.m_coord, this.m_endomorphism, this.m_multiplier);
		}

		public virtual ECPoint ValidatePoint(BigInteger x, BigInteger y)
		{
			ECPoint p = CreatePoint(x, y);
			if (!p.IsValid())
			{
				throw new ArgumentException("Invalid point coordinates");
			}
			return p;
		}

		[Obsolete("Per-point compression property will be removed")]
		public virtual ECPoint ValidatePoint(BigInteger x, BigInteger y, bool withCompression)
		{
			ECPoint p = CreatePoint(x, y, withCompression);
			if (!p.IsValid())
			{
				throw new ArgumentException("Invalid point coordinates");
			}
			return p;
		}

		public virtual ECPoint CreatePoint(BigInteger x, BigInteger y)
		{
#pragma warning disable
			return CreatePoint(x, y, false);
#pragma warning restore
		}

		[Obsolete("Per-point compression property will be removed")]
		public virtual ECPoint CreatePoint(BigInteger x, BigInteger y, bool withCompression)
		{
			return CreateRawPoint(FromBigInteger(x), FromBigInteger(y), withCompression);
		}

		protected abstract ECCurve CloneCurve();

		protected internal abstract ECPoint CreateRawPoint(ECFieldElement x, ECFieldElement y, bool withCompression);

		protected internal abstract ECPoint CreateRawPoint(ECFieldElement x, ECFieldElement y, ECFieldElement[] zs, bool withCompression);

		protected virtual ECMultiplier CreateDefaultMultiplier()
		{
			GlvEndomorphism glvEndomorphism = m_endomorphism as GlvEndomorphism;
			if (glvEndomorphism != null)
			{
				return new GlvMultiplier(this, glvEndomorphism);
			}

			return new WNafL2RMultiplier();
		}

		public virtual bool SupportsCoordinateSystem(int coord)
		{
			return coord == COORD_AFFINE;
		}

		public virtual PreCompInfo GetPreCompInfo(ECPoint point, string name)
		{
			CheckPoint(point);
			lock (point)
			{
				IDictionary table = point.m_preCompTable;
				return table == null ? null : (PreCompInfo)table[name];
			}
		}

		/**
         * Adds <code>PreCompInfo</code> for a point on this curve, under a given name. Used by
         * <code>ECMultiplier</code>s to save the precomputation for this <code>ECPoint</code> for use
         * by subsequent multiplication.
         * 
         * @param point
         *            The <code>ECPoint</code> to store precomputations for.
         * @param name
         *            A <code>String</code> used to index precomputations of different types.
         * @param preCompInfo
         *            The values precomputed by the <code>ECMultiplier</code>.
         */
		public virtual void SetPreCompInfo(ECPoint point, string name, PreCompInfo preCompInfo)
		{
			CheckPoint(point);
			lock (point)
			{
				IDictionary table = point.m_preCompTable;
				if (null == table)
				{
					point.m_preCompTable = table = Platform.CreateHashtable(4);
				}
				table[name] = preCompInfo;
			}
		}

		public virtual ECPoint ImportPoint(ECPoint p)
		{
			if (this == p.Curve)
			{
				return p;
			}
			if (p.IsInfinity)
			{
				return Infinity;
			}

			// TODO Default behaviour could be improved if the two curves have the same coordinate system by copying any Z coordinates.
			p = p.Normalize();
#pragma warning disable
			return ValidatePoint(p.XCoord.ToBigInteger(), p.YCoord.ToBigInteger(), p.IsCompressed);
#pragma warning restore
		}

		/**
         * Normalization ensures that any projective coordinate is 1, and therefore that the x, y
         * coordinates reflect those of the equivalent point in an affine coordinate system. Where more
         * than one point is to be normalized, this method will generally be more efficient than
         * normalizing each point separately.
         * 
         * @param points
         *            An array of points that will be updated in place with their normalized versions,
         *            where necessary
         */
		public virtual void NormalizeAll(ECPoint[] points)
		{
			NormalizeAll(points, 0, points.Length, null);
		}

		/**
         * Normalization ensures that any projective coordinate is 1, and therefore that the x, y
         * coordinates reflect those of the equivalent point in an affine coordinate system. Where more
         * than one point is to be normalized, this method will generally be more efficient than
         * normalizing each point separately. An (optional) z-scaling factor can be applied; effectively
         * each z coordinate is scaled by this value prior to normalization (but only one
         * actual multiplication is needed).
         * 
         * @param points
         *            An array of points that will be updated in place with their normalized versions,
         *            where necessary
         * @param off
         *            The start of the range of points to normalize
         * @param len
         *            The length of the range of points to normalize
         * @param iso
         *            The (optional) z-scaling factor - can be null
         */
		public virtual void NormalizeAll(ECPoint[] points, int off, int len, ECFieldElement iso)
		{
			CheckPoints(points, off, len);

			switch (this.CoordinateSystem)
			{
				case ECCurve.COORD_AFFINE:
				case ECCurve.COORD_LAMBDA_AFFINE:
					{
						if (iso != null)
							throw new ArgumentException("not valid for affine coordinates", "iso");

						return;
					}
			}

			/*
             * Figure out which of the points actually need to be normalized
             */
			ECFieldElement[] zs = new ECFieldElement[len];
			int[] indices = new int[len];
			int count = 0;
			for (int i = 0; i < len; ++i)
			{
				ECPoint p = points[off + i];
				if (null != p && (iso != null || !p.IsNormalized()))
				{
					zs[count] = p.GetZCoord(0);
					indices[count++] = off + i;
				}
			}

			if (count == 0)
			{
				return;
			}

			ECAlgorithms.MontgomeryTrick(zs, 0, count, iso);

			for (int j = 0; j < count; ++j)
			{
				int index = indices[j];
				points[index] = points[index].Normalize(zs[j]);
			}
		}

		public abstract ECPoint Infinity
		{
			get;
		}

		public virtual IFiniteField Field
		{
			get
			{
				return m_field;
			}
		}

		public virtual ECFieldElement A
		{
			get
			{
				return m_a;
			}
		}

		public virtual ECFieldElement B
		{
			get
			{
				return m_b;
			}
		}

		public virtual BigInteger Order
		{
			get
			{
				return m_order;
			}
		}

		public virtual BigInteger Cofactor
		{
			get
			{
				return m_cofactor;
			}
		}

		public virtual int CoordinateSystem
		{
			get
			{
				return m_coord;
			}
		}

		protected virtual void CheckPoint(ECPoint point)
		{
			if (null == point || (this != point.Curve))
				throw new ArgumentException("must be non-null and on this curve", "point");
		}

		protected virtual void CheckPoints(ECPoint[] points)
		{
			CheckPoints(points, 0, points.Length);
		}

		protected virtual void CheckPoints(ECPoint[] points, int off, int len)
		{
			if (points == null)
				throw new ArgumentNullException(nameof(points));
			if (off < 0 || len < 0 || (off > (points.Length - len)))
				throw new ArgumentException("invalid range specified", "points");

			for (int i = 0; i < len; ++i)
			{
				ECPoint point = points[off + i];
				if (null != point && this != point.Curve)
					throw new ArgumentException("entries must be null or on this curve", "points");
			}
		}

		public virtual bool Equals(ECCurve other)
		{
			if (this == other)
				return true;
			if (null == other)
				return false;
			return Field.Equals(other.Field)
				&& A.ToBigInteger().Equals(other.A.ToBigInteger())
				&& B.ToBigInteger().Equals(other.B.ToBigInteger());
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as ECCurve);
		}

		public override int GetHashCode()
		{
			throw new NotImplementedException();
		}

		protected abstract ECPoint DecompressPoint(int yTilde, BigInteger X1);

		public virtual ECEndomorphism GetEndomorphism()
		{
			return m_endomorphism;
		}

		/**
         * Sets the default <code>ECMultiplier</code>, unless already set. 
         */
		public virtual ECMultiplier GetMultiplier()
		{
			lock (this)
			{
				if (this.m_multiplier == null)
				{
					this.m_multiplier = CreateDefaultMultiplier();
				}
				return this.m_multiplier;
			}
		}

		/**
         * Decode a point on this curve from its ASN.1 encoding. The different
         * encodings are taken account of, including point compression for
         * <code>F<sub>p</sub></code> (X9.62 s 4.2.1 pg 17).
         * @return The decoded point.
         */
		public virtual ECPoint DecodePoint(byte[] encoded)
		{
			ECPoint p = null;
			int expectedLength = (FieldSize + 7) / 8;

			byte type = encoded[0];
			switch (type)
			{
				case 0x00: // infinity
					{
						if (encoded.Length != 1)
							throw new ArgumentException("Incorrect length for infinity encoding", "encoded");

						p = Infinity;
						break;
					}

				case 0x02: // compressed
				case 0x03: // compressed
					{
						if (encoded.Length != (expectedLength + 1))
							throw new ArgumentException("Incorrect length for compressed encoding", "encoded");

						int yTilde = type & 1;
						BigInteger X = new BigInteger(1, encoded, 1, expectedLength);

						p = DecompressPoint(yTilde, X);
						if (!p.SatisfiesCofactor())
							throw new ArgumentException("Invalid point");

						break;
					}

				case 0x04: // uncompressed
					{
						if (encoded.Length != (2 * expectedLength + 1))
							throw new ArgumentException("Incorrect length for uncompressed encoding", "encoded");

						BigInteger X = new BigInteger(1, encoded, 1, expectedLength);
						BigInteger Y = new BigInteger(1, encoded, 1 + expectedLength, expectedLength);

						p = ValidatePoint(X, Y);
						break;
					}

				case 0x06: // hybrid
				case 0x07: // hybrid
					{
						if (encoded.Length != (2 * expectedLength + 1))
							throw new ArgumentException("Incorrect length for hybrid encoding", "encoded");

						BigInteger X = new BigInteger(1, encoded, 1, expectedLength);
						BigInteger Y = new BigInteger(1, encoded, 1 + expectedLength, expectedLength);

						if (Y.TestBit(0) != (type == 0x07))
							throw new ArgumentException("Inconsistent Y coordinate in hybrid encoding", "encoded");

						p = ValidatePoint(X, Y);
						break;
					}

				default:
					throw new FormatException("Invalid point encoding " + type);
			}

			if (type != 0x00 && p.IsInfinity)
				throw new ArgumentException("Invalid infinity encoding", "encoded");

			return p;
		}
	}

	internal abstract class AbstractFpCurve
		: ECCurve
	{
		protected AbstractFpCurve(BigInteger q)
			: base(FiniteFields.GetPrimeField(q))
		{
		}

		public override bool IsValidFieldElement(BigInteger x)
		{
			return x != null && x.SignValue >= 0 && x.CompareTo(Field.Characteristic) < 0;
		}

		protected override ECPoint DecompressPoint(int yTilde, BigInteger X1)
		{
			ECFieldElement x = FromBigInteger(X1);
			ECFieldElement rhs = x.Square().Add(A).Multiply(x).Add(B);
			ECFieldElement y = rhs.Sqrt();

			/*
             * If y is not a square, then we haven't got a point on the curve
             */
			if (y == null)
				throw new ArgumentException("Invalid point compression");

			if (y.TestBitZero() != (yTilde == 1))
			{
				// Use the other root
				y = y.Negate();
			}

			return CreateRawPoint(x, y, true);
		}
	}

	/**
     * Elliptic curve over Fp
     */
	internal class FpCurve
		: AbstractFpCurve
	{
		private const int FP_DEFAULT_COORDS = COORD_JACOBIAN_MODIFIED;

		protected readonly BigInteger m_q, m_r;
		protected readonly FpPoint m_infinity;

		public FpCurve(BigInteger q, BigInteger a, BigInteger b)
			: this(q, a, b, null, null)
		{
		}

		public FpCurve(BigInteger q, BigInteger a, BigInteger b, BigInteger order, BigInteger cofactor)
			: base(q)
		{
			this.m_q = q;
			this.m_r = FpFieldElement.CalculateResidue(q);
			this.m_infinity = new FpPoint(this, null, null);

			this.m_a = FromBigInteger(a);
			this.m_b = FromBigInteger(b);
			this.m_order = order;
			this.m_cofactor = cofactor;
			this.m_coord = FP_DEFAULT_COORDS;
		}

		protected FpCurve(BigInteger q, BigInteger r, ECFieldElement a, ECFieldElement b)
			: this(q, r, a, b, null, null)
		{
		}

		protected FpCurve(BigInteger q, BigInteger r, ECFieldElement a, ECFieldElement b, BigInteger order, BigInteger cofactor)
			: base(q)
		{
			this.m_q = q;
			this.m_r = r;
			this.m_infinity = new FpPoint(this, null, null);

			this.m_a = a;
			this.m_b = b;
			this.m_order = order;
			this.m_cofactor = cofactor;
			this.m_coord = FP_DEFAULT_COORDS;
		}

		protected override ECCurve CloneCurve()
		{
			return new FpCurve(m_q, m_r, m_a, m_b, m_order, m_cofactor);
		}

		public override bool SupportsCoordinateSystem(int coord)
		{
			switch (coord)
			{
				case COORD_AFFINE:
				case COORD_HOMOGENEOUS:
				case COORD_JACOBIAN:
				case COORD_JACOBIAN_MODIFIED:
					return true;
				default:
					return false;
			}
		}

		public virtual BigInteger Q
		{
			get
			{
				return m_q;
			}
		}

		public override ECPoint Infinity
		{
			get
			{
				return m_infinity;
			}
		}

		public override int FieldSize
		{
			get
			{
				return m_q.BitLength;
			}
		}

		public override ECFieldElement FromBigInteger(BigInteger x)
		{
			return new FpFieldElement(this.m_q, this.m_r, x);
		}

		protected internal override ECPoint CreateRawPoint(ECFieldElement x, ECFieldElement y, bool withCompression)
		{
			return new FpPoint(this, x, y, withCompression);
		}

		protected internal override ECPoint CreateRawPoint(ECFieldElement x, ECFieldElement y, ECFieldElement[] zs, bool withCompression)
		{
			return new FpPoint(this, x, y, zs, withCompression);
		}

		public override ECPoint ImportPoint(ECPoint p)
		{
			if (this != p.Curve && this.CoordinateSystem == COORD_JACOBIAN && !p.IsInfinity)
			{
				switch (p.Curve.CoordinateSystem)
				{
					case COORD_JACOBIAN:
					case COORD_JACOBIAN_CHUDNOVSKY:
					case COORD_JACOBIAN_MODIFIED:
						return new FpPoint(this,
							FromBigInteger(p.RawXCoord.ToBigInteger()),
							FromBigInteger(p.RawYCoord.ToBigInteger()),
							new ECFieldElement[] { FromBigInteger(p.GetZCoord(0).ToBigInteger()) },
							p.IsCompressed);
					default:
						break;
				}
			}

			return base.ImportPoint(p);
		}
	}

	internal abstract class AbstractF2mCurve
		: ECCurve
	{
		public static BigInteger Inverse(int m, int[] ks, BigInteger x)
		{
			return new LongArray(x).ModInverse(m, ks).ToBigInteger();
		}

		/**
         * The auxiliary values <code>s<sub>0</sub></code> and
         * <code>s<sub>1</sub></code> used for partial modular reduction for
         * Koblitz curves.
         */
		private BigInteger[] si = null;

		private static IFiniteField BuildField(int m, int k1, int k2, int k3)
		{
			if (k1 == 0)
			{
				throw new ArgumentException("k1 must be > 0");
			}

			if (k2 == 0)
			{
				if (k3 != 0)
				{
					throw new ArgumentException("k3 must be 0 if k2 == 0");
				}

				return FiniteFields.GetBinaryExtensionField(new int[] { 0, k1, m });
			}

			if (k2 <= k1)
			{
				throw new ArgumentException("k2 must be > k1");
			}

			if (k3 <= k2)
			{
				throw new ArgumentException("k3 must be > k2");
			}

			return FiniteFields.GetBinaryExtensionField(new int[] { 0, k1, k2, k3, m });
		}

		protected AbstractF2mCurve(int m, int k1, int k2, int k3)
			: base(BuildField(m, k1, k2, k3))
		{
		}

		public override bool IsValidFieldElement(BigInteger x)
		{
			return x != null && x.SignValue >= 0 && x.BitLength <= FieldSize;
		}

		[Obsolete("Per-point compression property will be removed")]
		public override ECPoint CreatePoint(BigInteger x, BigInteger y, bool withCompression)
		{
			ECFieldElement X = FromBigInteger(x), Y = FromBigInteger(y);

			switch (this.CoordinateSystem)
			{
				case COORD_LAMBDA_AFFINE:
				case COORD_LAMBDA_PROJECTIVE:
					{
						if (X.IsZero)
						{
							if (!Y.Square().Equals(B))
								throw new ArgumentException();
						}
						else
						{
							// Y becomes Lambda (X + Y/X) here
							Y = Y.Divide(X).Add(X);
						}
						break;
					}
				default:
					{
						break;
					}
			}

			return CreateRawPoint(X, Y, withCompression);
		}

		protected override ECPoint DecompressPoint(int yTilde, BigInteger X1)
		{
			ECFieldElement xp = FromBigInteger(X1), yp = null;
			if (xp.IsZero)
			{
				yp = B.Sqrt();
			}
			else
			{
				ECFieldElement beta = xp.Square().Invert().Multiply(B).Add(A).Add(xp);
				ECFieldElement z = SolveQuadradicEquation(beta);

				if (z != null)
				{
					if (z.TestBitZero() != (yTilde == 1))
					{
						z = z.AddOne();
					}

					switch (this.CoordinateSystem)
					{
						case COORD_LAMBDA_AFFINE:
						case COORD_LAMBDA_PROJECTIVE:
							{
								yp = z.Add(xp);
								break;
							}
						default:
							{
								yp = z.Multiply(xp);
								break;
							}
					}
				}
			}

			if (yp == null)
				throw new ArgumentException("Invalid point compression");

			return CreateRawPoint(xp, yp, true);
		}

		/**
         * Solves a quadratic equation <code>z<sup>2</sup> + z = beta</code>(X9.62
         * D.1.6) The other solution is <code>z + 1</code>.
         *
         * @param beta
         *            The value to solve the quadratic equation for.
         * @return the solution for <code>z<sup>2</sup> + z = beta</code> or
         *         <code>null</code> if no solution exists.
         */
		private ECFieldElement SolveQuadradicEquation(ECFieldElement beta)
		{
			if (beta.IsZero)
				return beta;

			ECFieldElement gamma, z, zeroElement = FromBigInteger(BigInteger.Zero);

			int m = FieldSize;
			do
			{
				ECFieldElement t = FromBigInteger(BigInteger.Arbitrary(m));
				z = zeroElement;
				ECFieldElement w = beta;
				for (int i = 1; i < m; i++)
				{
					ECFieldElement w2 = w.Square();
					z = z.Square().Add(w2.Multiply(t));
					w = w2.Add(beta);
				}
				if (!w.IsZero)
				{
					return null;
				}
				gamma = z.Square().Add(z);
			}
			while (gamma.IsZero);

			return z;
		}

		/**
         * @return the auxiliary values <code>s<sub>0</sub></code> and
         * <code>s<sub>1</sub></code> used for partial modular reduction for
         * Koblitz curves.
         */
		internal virtual BigInteger[] GetSi()
		{
			if (si == null)
			{
				lock (this)
				{
					if (si == null)
					{
						si = Tnaf.GetSi(this);
					}
				}
			}
			return si;
		}

		/**
         * Returns true if this is a Koblitz curve (ABC curve).
         * @return true if this is a Koblitz curve (ABC curve), false otherwise
         */
		public virtual bool IsKoblitz
		{
			get
			{
				return m_order != null && m_cofactor != null && m_b.IsOne && (m_a.IsZero || m_a.IsOne);
			}
		}
	}

	/**
     * Elliptic curves over F2m. The Weierstrass equation is given by
     * <code>y<sup>2</sup> + xy = x<sup>3</sup> + ax<sup>2</sup> + b</code>.
     */
	internal class F2mCurve
		: AbstractF2mCurve
	{
		private const int F2M_DEFAULT_COORDS = COORD_LAMBDA_PROJECTIVE;

		/**
         * The exponent <code>m</code> of <code>F<sub>2<sup>m</sup></sub></code>.
         */
		private readonly int m;

		/**
         * TPB: The integer <code>k</code> where <code>x<sup>m</sup> +
         * x<sup>k</sup> + 1</code> represents the reduction polynomial
         * <code>f(z)</code>.<br/>
         * PPB: The integer <code>k1</code> where <code>x<sup>m</sup> +
         * x<sup>k3</sup> + x<sup>k2</sup> + x<sup>k1</sup> + 1</code>
         * represents the reduction polynomial <code>f(z)</code>.<br/>
         */
		private readonly int k1;

		/**
         * TPB: Always set to <code>0</code><br/>
         * PPB: The integer <code>k2</code> where <code>x<sup>m</sup> +
         * x<sup>k3</sup> + x<sup>k2</sup> + x<sup>k1</sup> + 1</code>
         * represents the reduction polynomial <code>f(z)</code>.<br/>
         */
		private readonly int k2;

		/**
         * TPB: Always set to <code>0</code><br/>
         * PPB: The integer <code>k3</code> where <code>x<sup>m</sup> +
         * x<sup>k3</sup> + x<sup>k2</sup> + x<sup>k1</sup> + 1</code>
         * represents the reduction polynomial <code>f(z)</code>.<br/>
         */
		private readonly int k3;

		/**
         * The point at infinity on this curve.
         */
		protected readonly F2mPoint m_infinity;

		/**
         * Constructor for Trinomial Polynomial Basis (TPB).
         * @param m  The exponent <code>m</code> of
         * <code>F<sub>2<sup>m</sup></sub></code>.
         * @param k The integer <code>k</code> where <code>x<sup>m</sup> +
         * x<sup>k</sup> + 1</code> represents the reduction
         * polynomial <code>f(z)</code>.
         * @param a The coefficient <code>a</code> in the Weierstrass equation
         * for non-supersingular elliptic curves over
         * <code>F<sub>2<sup>m</sup></sub></code>.
         * @param b The coefficient <code>b</code> in the Weierstrass equation
         * for non-supersingular elliptic curves over
         * <code>F<sub>2<sup>m</sup></sub></code>.
         */
		public F2mCurve(
			int m,
			int k,
			BigInteger a,
			BigInteger b)
			: this(m, k, 0, 0, a, b, null, null)
		{
		}

		/**
         * Constructor for Trinomial Polynomial Basis (TPB).
         * @param m  The exponent <code>m</code> of
         * <code>F<sub>2<sup>m</sup></sub></code>.
         * @param k The integer <code>k</code> where <code>x<sup>m</sup> +
         * x<sup>k</sup> + 1</code> represents the reduction
         * polynomial <code>f(z)</code>.
         * @param a The coefficient <code>a</code> in the Weierstrass equation
         * for non-supersingular elliptic curves over
         * <code>F<sub>2<sup>m</sup></sub></code>.
         * @param b The coefficient <code>b</code> in the Weierstrass equation
         * for non-supersingular elliptic curves over
         * <code>F<sub>2<sup>m</sup></sub></code>.
         * @param order The order of the main subgroup of the elliptic curve.
         * @param cofactor The cofactor of the elliptic curve, i.e.
         * <code>#E<sub>a</sub>(F<sub>2<sup>m</sup></sub>) = h * n</code>.
         */
		public F2mCurve(
			int m,
			int k,
			BigInteger a,
			BigInteger b,
			BigInteger order,
			BigInteger cofactor)
			: this(m, k, 0, 0, a, b, order, cofactor)
		{
		}

		/**
         * Constructor for Pentanomial Polynomial Basis (PPB).
         * @param m  The exponent <code>m</code> of
         * <code>F<sub>2<sup>m</sup></sub></code>.
         * @param k1 The integer <code>k1</code> where <code>x<sup>m</sup> +
         * x<sup>k3</sup> + x<sup>k2</sup> + x<sup>k1</sup> + 1</code>
         * represents the reduction polynomial <code>f(z)</code>.
         * @param k2 The integer <code>k2</code> where <code>x<sup>m</sup> +
         * x<sup>k3</sup> + x<sup>k2</sup> + x<sup>k1</sup> + 1</code>
         * represents the reduction polynomial <code>f(z)</code>.
         * @param k3 The integer <code>k3</code> where <code>x<sup>m</sup> +
         * x<sup>k3</sup> + x<sup>k2</sup> + x<sup>k1</sup> + 1</code>
         * represents the reduction polynomial <code>f(z)</code>.
         * @param a The coefficient <code>a</code> in the Weierstrass equation
         * for non-supersingular elliptic curves over
         * <code>F<sub>2<sup>m</sup></sub></code>.
         * @param b The coefficient <code>b</code> in the Weierstrass equation
         * for non-supersingular elliptic curves over
         * <code>F<sub>2<sup>m</sup></sub></code>.
         */
		public F2mCurve(
			int m,
			int k1,
			int k2,
			int k3,
			BigInteger a,
			BigInteger b)
			: this(m, k1, k2, k3, a, b, null, null)
		{
		}

		/**
         * Constructor for Pentanomial Polynomial Basis (PPB).
         * @param m  The exponent <code>m</code> of
         * <code>F<sub>2<sup>m</sup></sub></code>.
         * @param k1 The integer <code>k1</code> where <code>x<sup>m</sup> +
         * x<sup>k3</sup> + x<sup>k2</sup> + x<sup>k1</sup> + 1</code>
         * represents the reduction polynomial <code>f(z)</code>.
         * @param k2 The integer <code>k2</code> where <code>x<sup>m</sup> +
         * x<sup>k3</sup> + x<sup>k2</sup> + x<sup>k1</sup> + 1</code>
         * represents the reduction polynomial <code>f(z)</code>.
         * @param k3 The integer <code>k3</code> where <code>x<sup>m</sup> +
         * x<sup>k3</sup> + x<sup>k2</sup> + x<sup>k1</sup> + 1</code>
         * represents the reduction polynomial <code>f(z)</code>.
         * @param a The coefficient <code>a</code> in the Weierstrass equation
         * for non-supersingular elliptic curves over
         * <code>F<sub>2<sup>m</sup></sub></code>.
         * @param b The coefficient <code>b</code> in the Weierstrass equation
         * for non-supersingular elliptic curves over
         * <code>F<sub>2<sup>m</sup></sub></code>.
         * @param order The order of the main subgroup of the elliptic curve.
         * @param cofactor The cofactor of the elliptic curve, i.e.
         * <code>#E<sub>a</sub>(F<sub>2<sup>m</sup></sub>) = h * n</code>.
         */
		public F2mCurve(
			int m,
			int k1,
			int k2,
			int k3,
			BigInteger a,
			BigInteger b,
			BigInteger order,
			BigInteger cofactor)
			: base(m, k1, k2, k3)
		{
			this.m = m;
			this.k1 = k1;
			this.k2 = k2;
			this.k3 = k3;
			this.m_order = order;
			this.m_cofactor = cofactor;
			this.m_infinity = new F2mPoint(this, null, null);

			if (k1 == 0)
				throw new ArgumentException("k1 must be > 0");

			if (k2 == 0)
			{
				if (k3 != 0)
					throw new ArgumentException("k3 must be 0 if k2 == 0");
			}
			else
			{
				if (k2 <= k1)
					throw new ArgumentException("k2 must be > k1");

				if (k3 <= k2)
					throw new ArgumentException("k3 must be > k2");
			}

			this.m_a = FromBigInteger(a);
			this.m_b = FromBigInteger(b);
			this.m_coord = F2M_DEFAULT_COORDS;
		}

		protected F2mCurve(int m, int k1, int k2, int k3, ECFieldElement a, ECFieldElement b, BigInteger order, BigInteger cofactor)
			: base(m, k1, k2, k3)
		{
			this.m = m;
			this.k1 = k1;
			this.k2 = k2;
			this.k3 = k3;
			this.m_order = order;
			this.m_cofactor = cofactor;

			this.m_infinity = new F2mPoint(this, null, null);
			this.m_a = a;
			this.m_b = b;
			this.m_coord = F2M_DEFAULT_COORDS;
		}

		protected override ECCurve CloneCurve()
		{
			return new F2mCurve(m, k1, k2, k3, m_a, m_b, m_order, m_cofactor);
		}

		public override bool SupportsCoordinateSystem(int coord)
		{
			switch (coord)
			{
				case COORD_AFFINE:
				case COORD_HOMOGENEOUS:
				case COORD_LAMBDA_PROJECTIVE:
					return true;
				default:
					return false;
			}
		}

		protected override ECMultiplier CreateDefaultMultiplier()
		{
			if (IsKoblitz)
			{
				return new WTauNafMultiplier();
			}

			return base.CreateDefaultMultiplier();
		}

		public override int FieldSize
		{
			get
			{
				return m;
			}
		}

		public override ECFieldElement FromBigInteger(BigInteger x)
		{
			return new F2mFieldElement(this.m, this.k1, this.k2, this.k3, x);
		}

		protected internal override ECPoint CreateRawPoint(ECFieldElement x, ECFieldElement y, bool withCompression)
		{
			return new F2mPoint(this, x, y, withCompression);
		}

		protected internal override ECPoint CreateRawPoint(ECFieldElement x, ECFieldElement y, ECFieldElement[] zs, bool withCompression)
		{
			return new F2mPoint(this, x, y, zs, withCompression);
		}

		public override ECPoint Infinity
		{
			get
			{
				return m_infinity;
			}
		}

		public int M
		{
			get
			{
				return m;
			}
		}

		/**
         * Return true if curve uses a Trinomial basis.
         *
         * @return true if curve Trinomial, false otherwise.
         */
		public bool IsTrinomial()
		{
			return k2 == 0 && k3 == 0;
		}

		public int K1
		{
			get
			{
				return k1;
			}
		}

		public int K2
		{
			get
			{
				return k2;
			}
		}

		public int K3
		{
			get
			{
				return k3;
			}
		}

		[Obsolete("Use 'Order' property instead")]
		public BigInteger N
		{
			get
			{
				return m_order;
			}
		}

		[Obsolete("Use 'Cofactor' property instead")]
		public BigInteger H
		{
			get
			{
				return m_cofactor;
			}
		}
	}
}
