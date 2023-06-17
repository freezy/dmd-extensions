using LibDmd.Input;

namespace LibDmd.Output
{
	public interface IFrameEventDestination : IDestination
	{
		void OnFrameEventInit(FrameEventInit frameEventInit);
		void OnFrameEvent(FrameEvent frameEvent);
	}
}
