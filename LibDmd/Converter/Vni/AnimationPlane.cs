﻿namespace LibDmd.Converter.Vni
{
	public abstract class AnimationPlane
	{
		/// <summary>
		/// Type of plane
		/// </summary>
		public byte Marker { get; protected set; }
		public byte[] Plane { get; protected set; }
	}
}
