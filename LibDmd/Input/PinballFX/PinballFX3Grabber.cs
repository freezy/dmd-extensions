using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibDmd.Input.PinballFX
{
	public class PinballFX3Grabber : PinballFXGrabber
	{
		public override string Name { get; } = "Pinball FX3";

		protected override string GetProcessName()
		{
			return "Pinball FX3";
		}
	}
}
