﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibDmd.Input
{

	/// <summary>
	/// A source that is able to deliver 24-bit frames without conversion.
	/// </summary>
	public interface IAlphaNumericSource : ISource
	{
		/// <summary>
		/// Returns an observable that produces a sequence of alphanumeric frames.
		/// 
		/// </summary>
		/// <remarks>When disposed, frame production must stop.</remarks>
		IObservable<AlphaNumericFrame> GetAlphaNumericFrames();
	}
}
