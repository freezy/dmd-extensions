using System;
using System.Collections.Generic;
using System.Reactive;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Converter
{
	public abstract class AbstractConverter : AbstractSource
	{
		/// <summary>
		/// Source bit length
		/// </summary>
		public abstract IEnumerable<FrameFormat> From { get; }

		/// <summary>
		/// Receives frames and outputs them to the output sources the converter implements.
		/// </summary>
		///
		/// <remarks>
		/// Note that if your convertor doesn't implement any ISource interface,
		/// frames will just be dropped.
		///
		/// If this method doesn't send anything to its output sources, the frame is
		/// equally dropped.
		/// </remarks>
		///
		/// <param name="frame">Source frame</param>
		public abstract void Convert(DmdFrame frame);

		/// <summary>
		/// Receives alphanumeric frames and converts them into colored DMD frames (if supported).
		/// </summary>
		/// <param name="frame">Source frame</param>
		public virtual void Convert(AlphaNumericFrame frame)
		{
		}

		/// <summary>
		/// True if any of the converter's sources has been subscribed to.
		/// </summary>
		public bool IsConnected { get; protected set; }

		public IObservable<Unit> OnResume { get; } = null;
		public IObservable<Unit> OnPause { get; } = null;
	}
}
