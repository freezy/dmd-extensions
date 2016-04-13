using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PinDmd;
using PinDmd.Input;

namespace App
{
	/// <summary>
	/// Interaction logic for ScreenGrabber.xaml
	/// </summary>
	public partial class GrabberWindow : Window
	{
		private readonly ScreenGrabber _grabber;
		private readonly PinDmd.PinDmd _dmd;

		public GrabberWindow()
		{
			InitializeComponent();
			LocationChanged += Window_LocationChanged;
			IsVisibleChanged += ToggleGrabbing;

			Borders.MouseLeftButtonDown += MoveStart;
			Borders.MouseLeftButtonUp += MoveEnd;
			Borders.MouseMove += MoveMoving;

			_grabber = new ScreenGrabber { FramesPerSecond = 30 };
			_dmd = PinDmd.PinDmd.GetInstance();
		}

		/*
		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonDown(e);

			// Begin dragging the window
			DragMove();
		}*/

		private void ToggleGrabbing(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (IsVisible) {
				Console.WriteLine("Starting grabbing...");
				//_dmd.StartRendering(_grabber);
			} else {
				Console.WriteLine("Stopping grabbing...");
				//_dmd.StopRendering();
			}
		}

		private void Window_LocationChanged(object sender, EventArgs e)
		{
			_grabber.Left = (int)Left;
			_grabber.Top = (int)Top;
			_grabber.Width = (int)Width;
			_grabber.Height = (int)Height;
		}

		private bool _moving;
		private bool _resizing;
		private Point _resizeLastPos;
		private Point _moveLastPos;


		private void MoveStart(object sender, MouseButtonEventArgs e)
		{
			var rect = sender as Rectangle;
			if (rect != null) {
				_moving = true;
				_moveLastPos = PointToScreen(e.GetPosition(this));
				rect.CaptureMouse();
				Console.WriteLine("Down at {0} ({1})", rect.Name, e.GetPosition(this));
			}
		}
		private void MoveEnd(object sender, MouseButtonEventArgs e)
		{
			var rect = sender as Rectangle;
			if (rect != null) {
				_moving = false;
				rect.ReleaseMouseCapture();
				Console.WriteLine("Up at {0} ({1})", rect.Name, e.GetPosition(this));
			}
		}

		private void MoveMoving(object sender, MouseEventArgs e)
		{
			var rect = sender as Rectangle;
			if (rect != null && _moving) {
				var pos = PointToScreen(e.GetPosition(this));
				Top += pos.Y - _moveLastPos.Y;
				Left += pos.X - _moveLastPos.X;
				_moveLastPos = pos;
			}
		}

		private void ResizeStart(object sender, MouseButtonEventArgs e)
		{
			var rect = sender as Rectangle;
			if (rect != null) {
				_resizing = true;
				_resizeLastPos = PointToScreen(e.GetPosition(this));
				rect.CaptureMouse();
				Console.WriteLine("Down at {0} ({1})", rect.Name, e.GetPosition(this));
			}
		}

		private void ResizeEnd(object sender, MouseButtonEventArgs e)
		{
			var rect = sender as Rectangle;
			if (rect != null) {
				_resizing = false;
				rect.ReleaseMouseCapture();
				Console.WriteLine("Up at {0} ({1})", rect.Name, e.GetPosition(this));
			}
		}

		private void Resizing(object sender, MouseEventArgs e)
		{
			var rect = sender as Rectangle;
			if (rect != null && _resizing) {

				var pos = PointToScreen(e.GetPosition(this));
				Console.WriteLine("Moving to {0}", pos);

				if (rect.Name.ToLower().Contains("right")) {
					var width = Width + pos.X - _resizeLastPos.X;
					if (width > 0) {
						Width = width;
					}
				}

				if (rect.Name.ToLower().Contains("left")) {
					var left = Left + pos.X - _resizeLastPos.X;
					var width = Width + Left - left;
					if (left < Left + Width) {
						Width = width;
						Left = left;
					}
				}

				if (rect.Name.ToLower().Contains("bottom")) {
					var height = Height + pos.Y - _resizeLastPos.Y;
					if (height > 0) {
						Height = height;
					}
				}

				if (rect.Name.ToLower().Contains("top")) {
					var top = Top + pos.Y - _resizeLastPos.Y;
					var height = Height + Top - top;
					if (top < Top + Height) {
						Height = height;
						Top = top;
					}
				}

				_resizeLastPos = pos;
			}
		}
	}
}
