using System;
using System.Globalization;

namespace Civic.Simulation
{
    [Serializable]
    public readonly struct CivicNumber : IComparable<CivicNumber>, IEquatable<CivicNumber>
    {
        private const double Epsilon = 1e-12d;

        public static readonly CivicNumber Zero = new CivicNumber(0d, 0, false);
        public static readonly CivicNumber One = new CivicNumber(1d, 0, true);

        public CivicNumber(double mantissa, int exponent)
            : this(mantissa, exponent, true)
        {
        }

        private CivicNumber(double mantissa, int exponent, bool normalize)
        {
            if (normalize)
            {
                NormalizeParts(mantissa, exponent, out mantissa, out exponent);
            }

            Mantissa = mantissa;
            Exponent = exponent;
        }

        public double Mantissa { get; }
        public int Exponent { get; }
        public bool IsZero => Math.Abs(Mantissa) < Epsilon;
        public bool IsNegative => Mantissa < -Epsilon;

        public static CivicNumber FromDouble(double value)
        {
            if (Math.Abs(value) < Epsilon)
            {
                return Zero;
            }

            var exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            var mantissa = value / Math.Pow(10d, exponent);
            return new CivicNumber(mantissa, exponent);
        }

        public static CivicNumber Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Zero;
            }

            if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new FormatException($"Invalid CivicNumber: {value}");
            }

            return FromDouble(parsed);
        }

        public double ToDouble()
        {
            if (IsZero)
            {
                return 0d;
            }

            return Mantissa * Math.Pow(10d, Exponent);
        }

        public string ToShortString()
        {
            var value = ToDouble();
            var abs = Math.Abs(value);
            if (abs < 1000d)
            {
                return value.ToString(abs < 10d ? "0.##" : "0.#", CultureInfo.InvariantCulture);
            }

            var suffixes = new[] { "", "K", "M", "B", "T", "Qa", "Qi" };
            var suffixIndex = Math.Min((int)(Math.Log10(abs) / 3d), suffixes.Length - 1);
            var scaled = value / Math.Pow(1000d, suffixIndex);
            return scaled.ToString(Math.Abs(scaled) < 10d ? "0.#" : "0", CultureInfo.InvariantCulture) + suffixes[suffixIndex];
        }

        public int CompareTo(CivicNumber other)
        {
            if (IsZero && other.IsZero)
            {
                return 0;
            }

            if (IsZero)
            {
                return other.IsNegative ? 1 : -1;
            }

            if (other.IsZero)
            {
                return IsNegative ? -1 : 1;
            }

            if (IsNegative != other.IsNegative)
            {
                return IsNegative ? -1 : 1;
            }

            if (Exponent != other.Exponent)
            {
                var result = Exponent.CompareTo(other.Exponent);
                return IsNegative ? -result : result;
            }

            return Mantissa.CompareTo(other.Mantissa);
        }

        public bool Equals(CivicNumber other)
        {
            return CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            return obj is CivicNumber other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Mantissa, Exponent);
        }

        public override string ToString()
        {
            return IsZero ? "0" : $"{Mantissa.ToString("0.############", CultureInfo.InvariantCulture)}e{Exponent.ToString(CultureInfo.InvariantCulture)}";
        }

        public static CivicNumber Max(CivicNumber left, CivicNumber right)
        {
            return left.CompareTo(right) >= 0 ? left : right;
        }

        public static CivicNumber Min(CivicNumber left, CivicNumber right)
        {
            return left.CompareTo(right) <= 0 ? left : right;
        }

        public static CivicNumber ClampMinZero(CivicNumber value)
        {
            return value.CompareTo(Zero) < 0 ? Zero : value;
        }

        public static CivicNumber operator +(CivicNumber left, CivicNumber right)
        {
            if (left.IsZero)
            {
                return right;
            }

            if (right.IsZero)
            {
                return left;
            }

            var exponentDifference = left.Exponent - right.Exponent;
            if (Math.Abs(exponentDifference) > 15)
            {
                return exponentDifference > 0 ? left : right;
            }

            var exponent = Math.Max(left.Exponent, right.Exponent);
            var mantissa =
                left.Mantissa * Math.Pow(10d, left.Exponent - exponent) +
                right.Mantissa * Math.Pow(10d, right.Exponent - exponent);
            return new CivicNumber(mantissa, exponent);
        }

        public static CivicNumber operator -(CivicNumber left, CivicNumber right)
        {
            return left + new CivicNumber(-right.Mantissa, right.Exponent);
        }

        public static CivicNumber operator *(CivicNumber left, CivicNumber right)
        {
            if (left.IsZero || right.IsZero)
            {
                return Zero;
            }

            return new CivicNumber(left.Mantissa * right.Mantissa, left.Exponent + right.Exponent);
        }

        public static CivicNumber operator *(CivicNumber left, double right)
        {
            return left.IsZero || Math.Abs(right) < Epsilon ? Zero : new CivicNumber(left.Mantissa * right, left.Exponent);
        }

        public static CivicNumber operator *(double left, CivicNumber right)
        {
            return right * left;
        }

        public static CivicNumber operator /(CivicNumber left, CivicNumber right)
        {
            if (right.IsZero)
            {
                throw new DivideByZeroException();
            }

            return left.IsZero ? Zero : new CivicNumber(left.Mantissa / right.Mantissa, left.Exponent - right.Exponent);
        }

        public static CivicNumber operator /(CivicNumber left, double right)
        {
            if (Math.Abs(right) < Epsilon)
            {
                throw new DivideByZeroException();
            }

            return left.IsZero ? Zero : new CivicNumber(left.Mantissa / right, left.Exponent);
        }

        public static bool operator >(CivicNumber left, CivicNumber right) => left.CompareTo(right) > 0;
        public static bool operator <(CivicNumber left, CivicNumber right) => left.CompareTo(right) < 0;
        public static bool operator >=(CivicNumber left, CivicNumber right) => left.CompareTo(right) >= 0;
        public static bool operator <=(CivicNumber left, CivicNumber right) => left.CompareTo(right) <= 0;

        private static void NormalizeParts(double sourceMantissa, int sourceExponent, out double normalizedMantissa, out int normalizedExponent)
        {
            if (Math.Abs(sourceMantissa) < Epsilon)
            {
                normalizedMantissa = 0d;
                normalizedExponent = 0;
                return;
            }

            var mantissa = sourceMantissa;
            var exponent = sourceExponent;
            while (Math.Abs(mantissa) >= 10d)
            {
                mantissa /= 10d;
                exponent++;
            }

            while (Math.Abs(mantissa) < 1d)
            {
                mantissa *= 10d;
                exponent--;
            }

            normalizedMantissa = mantissa;
            normalizedExponent = exponent;
        }
    }
}
