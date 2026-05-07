namespace LibDmd.Input.PinballFX
{
	public class PinballFX3Grabber : PinballFXGrabber
	{
		public override string Name { get; } = "Pinball FX3";

		protected override System.Collections.Generic.IEnumerable<string> GetProcessNames()
		{
			return new[] { "Pinball FX3", "Pinball FX Classic" };
		}
	}
}
