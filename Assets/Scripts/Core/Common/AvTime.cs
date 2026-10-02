using System;
using System.Globalization;
using System.Numerics;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// An exact amount or moment of action value (AV), kept as a reduced fraction and never rounded, so speed
    /// breakpoints land precisely and seeded runs replay identically on every platform. One turn at Speed S lasts
    /// 10000 / S AV. Only <see cref="ToDouble"/> approximates, and only for display.
    /// </summary>
    public readonly struct AvTime : IEquatable<AvTime>, IComparable<AvTime>
    {
        readonly BigInteger numerator;
        readonly BigInteger denominatorMinusOne; // Stored minus one so default(AvTime) is a valid zero (0/1).

        public AvTime(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero) throw new DivideByZeroException("AvTime denominator is zero.");
            if (denominator.Sign < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }
            var divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
            if (!divisor.IsOne)
            {
                numerator /= divisor;
                denominator /= divisor;
            }
            this.numerator = numerator;
            denominatorMinusOne = denominator - BigInteger.One;
        }

        public static readonly AvTime Zero = default;

        public static AvTime FromWhole(long av) => new AvTime(av, BigInteger.One);

        public BigInteger Numerator => numerator;
        public BigInteger Denominator => denominatorMinusOne + BigInteger.One;

        public static AvTime operator +(AvTime a, AvTime b) =>
            new AvTime(a.numerator * b.Denominator + b.numerator * a.Denominator, a.Denominator * b.Denominator);

        public static AvTime operator -(AvTime a, AvTime b) =>
            new AvTime(a.numerator * b.Denominator - b.numerator * a.Denominator, a.Denominator * b.Denominator);

        /// <summary>This value times multiply / divide, exactly (e.g. old speed / new speed).</summary>
        public AvTime Scale(long multiply, long divide) => new AvTime(numerator * multiply, Denominator * divide);

        /// <summary>Largest whole number not above this value.</summary>
        public BigInteger Floor() => BigInteger.Divide(numerator - (numerator.Sign < 0 ? Denominator - BigInteger.One : BigInteger.Zero), Denominator);

        /// <summary>Smallest whole number not below this value.</summary>
        public BigInteger Ceiling() => -new AvTime(-numerator, Denominator).Floor();

        public int CompareTo(AvTime other) => (numerator * other.Denominator).CompareTo(other.numerator * Denominator);
        public bool Equals(AvTime other) => numerator == other.numerator && denominatorMinusOne == other.denominatorMinusOne;
        public override bool Equals(object obj) => obj is AvTime other && Equals(other);
        public override int GetHashCode() => unchecked(numerator.GetHashCode() * 397 ^ denominatorMinusOne.GetHashCode());

        public static bool operator ==(AvTime a, AvTime b) => a.Equals(b);
        public static bool operator !=(AvTime a, AvTime b) => !a.Equals(b);
        public static bool operator <(AvTime a, AvTime b) => a.CompareTo(b) < 0;
        public static bool operator >(AvTime a, AvTime b) => a.CompareTo(b) > 0;
        public static bool operator <=(AvTime a, AvTime b) => a.CompareTo(b) <= 0;
        public static bool operator >=(AvTime a, AvTime b) => a.CompareTo(b) >= 0;

        /// <summary>Approximate value for display and logs. Never use it for game decisions.</summary>
        public double ToDouble() => (double)numerator / (double)Denominator;

        public override string ToString() => ToDouble().ToString("0.##", CultureInfo.InvariantCulture);
    }
}
