using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LibDmd.Frame;
using LibDmd.Input;
using LibDmd.Input.Network;
using LibDmd.Output;
using LibDmd.Output.Network;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class WebsocketServerTests
	{
		private WebsocketServer _server;
		private RenderGraphCollection _graphs;
		private List<IDestination> _renderers;
		private int _port;

		[SetUp]
		public void SetUp()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			_port = ((IPEndPoint)listener.LocalEndpoint).Port;
			listener.Stop();
			_server = new WebsocketServer("127.0.0.1", _port, "/dmd");
			_graphs = new RenderGraphCollection();
			_renderers = new List<IDestination>();
			_server.SetupGraphs(_graphs, _renderers);
		}

		[TearDown]
		public void TearDown()
		{
			_server.Dispose();
			_graphs.Dispose();
		}

		[TestCase("gray2Planes", 2)]
		[TestCase("gray4Planes", 4)]
		[TestCase("coloredGray2", 2)]
		[TestCase("coloredGray4", 4)]
		[TestCase("rgb24", 24)]
		public void ShouldPreserveWireDimensionsAndPixels(string format, int bitLength)
		{
			var decoder = new WebsocketSerializer();
			DmdFrame received = null;
			using (Frames(format).Subscribe(frame => received = frame)) {
				decoder.Unserialize(DimensionsMessage(128, 32), _server);
				decoder.Unserialize(FrameMessage(format, 128, 32, bitLength), _server);
			}

			AssertFrame(received, 128, 32, bitLength);
			if (received is ColoredFrame colored) {
				colored.Palette.Should().HaveCount(1 << bitLength);
				colored.Palette.Last().R.Should().Be((byte)((1 << bitLength) - 1));
			}
		}

		[Test]
		public void ShouldKeepEarlierFramesIntactAcrossDimensionAndFormatChanges()
		{
			var decoder = new WebsocketSerializer();
			var received = new List<DmdFrame>();
			using (Frames("gray2Planes").Merge(Frames("rgb24")).Subscribe(received.Add)) {
				decoder.Unserialize(DimensionsMessage(128, 32), _server);
				decoder.Unserialize(FrameMessage("gray2Planes", 128, 32, 2), _server);
				decoder.Unserialize(DimensionsMessage(192, 64), _server);
				decoder.Unserialize(FrameMessage("rgb24", 192, 64, 24), _server);
			}

			received.Should().HaveCount(2);
			AssertFrame(received[0], 128, 32, 2);
			AssertFrame(received[1], 192, 64, 24);
			received[0].Should().NotBeSameAs(received[1]);
		}

		[Test]
		public void ShouldKeepColoredFramesIntactAcrossDimensionChanges()
		{
			var decoder = new WebsocketSerializer();
			var received = new List<DmdFrame>();
			using (Frames("coloredGray4").Subscribe(received.Add)) {
				decoder.Unserialize(DimensionsMessage(128, 32), _server);
				decoder.Unserialize(FrameMessage("coloredGray4", 128, 32, 4), _server);
				decoder.Unserialize(DimensionsMessage(192, 64), _server);
				decoder.Unserialize(FrameMessage("coloredGray4", 192, 64, 4), _server);
			}

			received.Should().HaveCount(2);
			AssertFrame(received[0], 128, 32, 4);
			AssertFrame(received[1], 192, 64, 4);
			received[0].Should().NotBeSameAs(received[1]);
		}

		[Test]
		public void ShouldUseStandardDimensionsBeforeAnyDimensionsMessage()
		{
			DmdFrame received = null;
			using (Frames("gray2Planes").Subscribe(frame => received = frame)) {
				new WebsocketSerializer().Unserialize(FrameMessage("gray2Planes", 128, 32, 2), _server);
			}
			AssertFrame(received, 128, 32, 2);
		}

		[Test]
		public async Task ShouldDeliverA4096PixelFrameThroughTheRenderGraph()
		{
			var destination = new DestinationDynamicGray2();
			_renderers.Add(destination);
			var context = SynchronizationContext.Current;
			try {
				_graphs.StartRendering();
			} finally {
				// Graph startup installs a WPF context; this headless test has no UI pump.
				SynchronizationContext.SetSynchronizationContext(context);
			}
			var completion = new TaskCompletionSource<DmdFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
			using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
			using (timeout.Token.Register(() => completion.TrySetCanceled()))
			using (destination.Frame.Subscribe(frame => completion.TrySetResult(frame))) {
				var decoder = new WebsocketSerializer();
				decoder.Unserialize(DimensionsMessage(128, 32), _server);
				decoder.Unserialize(FrameMessage("gray2Planes", 128, 32, 2), _server);
				AssertFrame(await completion.Task, 128, 32, 2);
			}
		}

		[Test]
		public async Task ShouldKeepDimensionsWithEachWebsocketConnection()
		{
			using (var first = new ClientWebSocket())
			using (var second = new ClientWebSocket())
			using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10))) {
				var uri = new Uri($"ws://127.0.0.1:{_port}/dmd");
				await first.ConnectAsync(uri, timeout.Token);
				await second.ConnectAsync(uri, timeout.Token);
				await Send(first, DimensionsMessage(128, 32), timeout.Token);
				var firstFrame = await SendAndReceive(first, FrameMessage("gray2Planes", 128, 32, 2), timeout.Token);
				await Send(second, DimensionsMessage(192, 64), timeout.Token);
				var secondFrame = await SendAndReceive(second, FrameMessage("gray2Planes", 192, 64, 2), timeout.Token);
				// The other client's dimensions must not replace the first client's state.
				var nextFirstFrame = await SendAndReceive(first, FrameMessage("gray2Planes", 128, 32, 2), timeout.Token);
				await first.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", timeout.Token);
				await second.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", timeout.Token);

				AssertFrame(firstFrame, 128, 32, 2);
				AssertFrame(secondFrame, 192, 64, 2);
				AssertFrame(nextFirstFrame, 128, 32, 2);
			}
		}

		private async Task<DmdFrame> SendAndReceive(ClientWebSocket client, byte[] message, CancellationToken token)
		{
			var completion = new TaskCompletionSource<DmdFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
			using (token.Register(() => completion.TrySetCanceled()))
			using (Frames("gray2Planes").Take(1).Subscribe(frame => completion.TrySetResult(frame))) {
				await Send(client, message, token);
				return await completion.Task;
			}
		}

		private static Task Send(ClientWebSocket client, byte[] message, CancellationToken token)
			=> client.SendAsync(new ArraySegment<byte>(message), WebSocketMessageType.Binary, true, token);

		private IObservable<DmdFrame> Frames(string format)
		{
			var sources = _graphs.Graphs.Select(graph => graph.Source).ToArray();
			switch (format) {
				case "gray2Planes": return sources.OfType<IGray2Source>().Single().GetGray2Frames(false, false);
				case "gray4Planes": return sources.OfType<IGray4Source>().Single().GetGray4Frames(false, false);
				case "coloredGray2": return sources.OfType<IColoredGray2Source>().Single().GetColoredGray2Frames();
				case "coloredGray4": return sources.OfType<IColoredGray4Source>().Single().GetColoredGray4Frames();
				case "rgb24": return sources.OfType<IRgb24Source>().Single().GetRgb24Frames();
				default: throw new ArgumentOutOfRangeException(nameof(format));
			}
		}

		// Build the wire messages independently of the production serializer. The
		// sender uses null-terminated names and little-endian 32-bit values.
		private static byte[] DimensionsMessage(int width, int height) => Message("dimensions", writer => {
			writer.Write(width);
			writer.Write(height);
		});

		private static byte[] FrameMessage(string format, int width, int height, int bitLength) => Message(format, writer => {
			writer.Write((uint)1234);
			if (format.StartsWith("colored")) {
				writer.Write(1 << bitLength);
				for (var i = 0; i < (1 << bitLength); i++) {
					writer.Write(i << 16);
				}
			}
			var length = bitLength == 24 ? width * height * 3 : width * height / 8 * bitLength;
			writer.Write(Enumerable.Repeat((byte)255, length).ToArray());
		});

		private static byte[] Message(string name, Action<BinaryWriter> payload)
		{
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream)) {
				writer.Write(Encoding.ASCII.GetBytes(name));
				writer.Write((byte)0);
				payload(writer);
				return stream.ToArray();
			}
		}

		private static void AssertFrame(DmdFrame frame, int width, int height, int bitLength)
		{
			frame.Should().NotBeNull("the frame must reach the matching source");
			frame.Dimensions.Should().Be(new Dimensions(width, height));
			frame.BitLength.Should().Be(bitLength);
			frame.Data.Should().HaveCount(width * height * (bitLength == 24 ? 3 : 1))
				.And.OnlyContain(value => value == (bitLength == 24 ? 255 : (1 << bitLength) - 1));
		}
	}
}
