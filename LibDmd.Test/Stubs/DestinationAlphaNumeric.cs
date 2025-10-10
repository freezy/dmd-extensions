using LibDmd.Output;
using NLog;

namespace LibDmd.Test.Stubs
{
	public class DestinationAlphaNumeric : IAlphaNumericDestination
	{
		public string Name => "Destination[Fixed/Gray2]";
		public bool IsAvailable => true;
		public bool NeedsDuplicateFrames => false;
		public bool NeedsIdentificationFrames => false;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public void RenderAlphaNumeric(AlphaNumericFrame frame)
		{
			Logger.Info("[DestinationAlphaNumeric] New Frame!");
		}

		public void ClearDisplay()
		{
		}

		public void Dispose()
		{
		}
	}
}
