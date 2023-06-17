using System;

namespace LibDmd.Input
{
	public interface IFrameEventSource : ISource
	{
		IObservable<FrameEventInit> GetFrameEventInit();
		IObservable<FrameEvent> GetFrameEvents();
	}

	public class FrameEventInit : ICloneable
	{
		public readonly bool EventsAvailable;

		public FrameEventInit(bool eventsAvailable)
		{
			EventsAvailable = eventsAvailable;
		}

		public object Clone() => new FrameEventInit(EventsAvailable);
	}

	public class FrameEvent : ICloneable
	{
		public ushort EventId;

		public FrameEvent()
		{
		}

		public FrameEvent(ushort eventId)
		{
			EventId = eventId;
		}

		public FrameEvent Update(ushort eventId)
		{
			EventId = eventId;
			return this;
		}

		public object Clone() => new FrameEvent(EventId);
	}
}
