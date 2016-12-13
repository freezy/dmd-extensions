using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibDmd.Common;
using NLog;

namespace LibDmd.Converter.Colorize
{
	public class Frame
	{
		public uint Delay;
		public readonly byte[][] Planes;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Frame(BinaryReader reader)
		{
			Delay = reader.ReadUInt32BE();
			var numPlanes = reader.ReadUInt16BE();
			var planeSize = reader.ReadUInt16BE();
			//Logger.Trace("  [{2}] [fsq] Reading {0}-bit frame with {1}-byte planes", numPlanes, planeSize, reader.BaseStream.Position);

			Planes = new byte[numPlanes][];
			for (var i = 0; i < numPlanes; i++) {
				Planes[i] = reader.ReadBytes(planeSize);
			}
		}
	}
}
