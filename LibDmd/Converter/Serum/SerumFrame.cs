using System;
// ReSharper disable UnassignedField.Global

namespace LibDmd.Converter.Serum
{
	public struct SerumFrame
	{
		// data for v1 Serum format
		/// <summary>
		/// The colorized frame data.
		/// </summary>
		/// <remarks>Serum V1 only.</remarks>
		public IntPtr Frame;

		/// <summary>
		/// Palette of the colorized frame.
		/// </summary>
		/// <remarks>Serum V1 only.</remarks>
		public IntPtr Palette; // and its palette

		/// <summary>
		/// Rotations of the colorized frame.
		/// </summary>
		/// <remarks>Serum V1 only.</remarks>
		public IntPtr Rotations; // and its color rotations

		// data for v2 Serum format
		// the frame (frame32 or frame64) corresponding to the resolution of the ROM must ALWAYS be defined
		// if a frame pointer is defined, its width, rotations and rotationsinframe pointers must be defined
		public IntPtr frame32;
		public uint width32; // 0 is returned if the 32p colorized frame is not available for this frame
		public IntPtr rotations32;
		public IntPtr rotationsinframe32; // [width32*32*2] precalculated array to tell if a color is in a color rotations of the frame ([X*Y*0]=0xffff if not part of a rotation)
		public IntPtr modifiedelements32; // (optional) 32P pixels modified during the last rotation
		public IntPtr frame64;
		public uint width64; // 0 is returned if the 64p colorized frame is not available for this frame
		public IntPtr rotations64;
		public IntPtr rotationsinframe64;  // [width64*64*2] precalculated array to tell if a color is in a color rotations of the frame ([X*Y*0]=0xffff if not part of a rotation)
		public IntPtr modifiedelements64; // (optional) 64P pixels modified during the last rotation
		// common data
		public uint SerumVersion; // SERUM_V1 or SERUM_V2
		/// <summary>
		/// flags for return:
		/// if flags & 1 : frame32 has been filled
		/// if flags & 2 : frame64 has been filled
		/// if flags & 4 : frame + palette have been filled
		/// if none of them, display the original frame
		/// </summary>
		public byte flags;
		public uint nocolors; // number of shades of orange in the ROM
		public uint ntriggers; // number of triggers in the Serum file
		public uint triggerID; // return 0xffff if no trigger for that frame, the ID of the trigger if one is set for that frame
		public uint frameID; // for CDMD ingame tester
		public ushort rotationtimer;
	}
}
