using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ProPinballDmdSlave
{
	class Program
	{
		static void Main(string[] args)
		{
			const int frameSize = 128*32;
			var bridge = new ProPinballBridge.ProPinballDmd(392);

			Console.WriteLine("Bridge status: {0}", bridge.Status);
			if (bridge.Status != 0) {
				unsafe
				{
					Console.WriteLine("Error: {0}", new string(bridge.Error));
				}
			} else {
				Console.WriteLine("Subscribing to Pro Pinball's message queue...");
				unsafe
				{
					bridge.GetFrames(frame => {
						Console.WriteLine("Got frame!");
						var f = new byte[frameSize];
						Marshal.Copy((IntPtr)frame, f, 0, frameSize);

					}, err => {
						Console.WriteLine("Error: {0}", new string(err));

					}, () => {
						Console.WriteLine("Done!");
					});
				}
			}
		}
	}
}
