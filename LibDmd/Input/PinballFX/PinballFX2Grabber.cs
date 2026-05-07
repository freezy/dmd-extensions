namespace LibDmd.Input.PinballFX
{
	public class PinballFX2Grabber : PinballFXGrabber
	{
		public override string Name { get; } = "Pinball FX2";

		protected override System.Collections.Generic.IEnumerable<string> GetProcessNames()
		{
			return new[] { "Pinball FX2" };
		}
	}
}
