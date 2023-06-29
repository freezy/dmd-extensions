using System;
using System.Collections.Generic;
using LibDmd.DmdDevice;
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace LibDmd
{
	public class AlphaNumericFrame : ICloneable, IEqualityComparer<AlphaNumericFrame>
	{
		/// <summary>
		/// The segment data
		/// </summary>
		public ushort[] SegmentData { get; private set; }

		/// <summary>
		/// The extended segment data
		/// </summary>
		public ushort[] SegmentDataExtended { get; private set; }

		/// <summary>
		/// The segment type
		/// </summary>
		public NumericalLayout SegmentLayout { get; private set; }

		public static bool operator == (AlphaNumericFrame x, AlphaNumericFrame y) => Equals(x, y);
		public static bool operator != (AlphaNumericFrame x, AlphaNumericFrame y) => !Equals(x, y);

		public AlphaNumericFrame()
		{
		}

		public AlphaNumericFrame(NumericalLayout layout, ushort[] segData)
		{
			SegmentData = segData;
			SegmentLayout = layout;
		}

		public AlphaNumericFrame(NumericalLayout layout, ushort[] segData, ushort[] segDataExtended)
		{
			SegmentData = segData;
			SegmentDataExtended = segDataExtended;
			SegmentLayout = layout;
		}


		public void Update(AlphaNumericFrame frame)
		{
			SegmentData = frame.SegmentData;
			SegmentDataExtended = frame.SegmentDataExtended;
			SegmentLayout = frame.SegmentLayout;
		}

		public object Clone() => new AlphaNumericFrame(SegmentLayout, SegmentData, SegmentDataExtended);

		#region Equality

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) {
				return false;
			}

			if (ReferenceEquals(this, obj)) {
				return true;
			}

			if (obj.GetType() != this.GetType()) {
				return false;
			}

			return Equals(this, (AlphaNumericFrame)obj);
		}

		protected bool Equals(AlphaNumericFrame other) => Equals(this, other);

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (SegmentData != null ? SegmentData.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (SegmentDataExtended != null ? SegmentDataExtended.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (int)SegmentLayout;
				return hashCode;
			}
		}

		public static bool Equals(AlphaNumericFrame x, AlphaNumericFrame y)
		{
			if (ReferenceEquals(x, y)) {
				return true;
			}

			if (ReferenceEquals(x, null)) {
				return false;
			}

			if (ReferenceEquals(y, null)) {
				return false;
			}

			if (x.GetType() != y.GetType()) {
				return false;
			}

			return Compare(x.SegmentData, y.SegmentData)
			       && Compare(x.SegmentDataExtended, y.SegmentDataExtended)
			       && x.SegmentLayout == y.SegmentLayout;
		}

		bool IEqualityComparer<AlphaNumericFrame>.Equals(AlphaNumericFrame x, AlphaNumericFrame y) => Equals(x, y);

		private static bool Compare(IReadOnlyList<ushort> a, IReadOnlyList<ushort> b)
		{
			if (a == null && b == null) {
				return true;
			}

			if (a == null || b == null) {
				return false;
			}

			if (a.Count != b.Count) {
				return false;
			}

			for (var i = 0; i < a.Count; i++) {
				if (a[i] != b[i]) {
					return false;
				}
			}

			return true;
		}

		public int GetHashCode(AlphaNumericFrame obj)
		{
			unchecked
			{
				var hashCode = (obj.SegmentData != null ? obj.SegmentData.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (obj.SegmentDataExtended != null ? obj.SegmentDataExtended.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (int)obj.SegmentLayout;
				return hashCode;
			}
		}

		#endregion
	}
}
