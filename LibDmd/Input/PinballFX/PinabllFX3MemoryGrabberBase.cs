using System;
using System.Diagnostics;

namespace LibDmd.Input.PinballFX
{
	public abstract class PinabllFX3MemoryGrabberBase<FrameType>: MemoryGrabber<FrameType>
	{
		protected override IntPtr AttachGameProcess(Process p)
		{
			if (p.ProcessName == "Pinball FX3")
			{
				return GetPointerBaseAddress(p);
			}
			return IntPtr.Zero;
		}

		abstract protected IntPtr GetPointerBaseAddress(Process gameProc);
	}
}
