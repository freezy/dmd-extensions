using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;

namespace PinDmd.Input.ScreenGrabber
{
	/// <summary>
	/// A movable and resizable red border.
	/// </summary>
	/// <remarks>
	/// Independent from the actual screen grabber, subscribe to <see cref="WhenPositionChanges"/>
	/// and hook it up.
	/// </remarks>
	public partial class GrabberWindow : Window
	{
		public IObservable<System.Drawing.Rectangle> WhenPositionChanges => _whenPositionChanges;
		
		private readonly Subject<System.Drawing.Rectangle> _whenPositionChanges = new Subject<System.Drawing.Rectangle>();

		public GrabberWindow()
		{
			InitializeComponent();
			SizeChanged += PositionChanged;
			LocationChanged += PositionChanged;
			KeyDown += HotKey;
			KeyUp += HotKey;

			Borders.MouseLeftButtonDown += MoveStart;
			Borders.MouseLeftButtonUp += MoveEnd;
			Borders.MouseMove += MoveMoving;
		}

		private void PositionChanged(object sender, EventArgs e)
		{
			_whenPositionChanges.OnNext(new System.Drawing.Rectangle {
				X = (int)Left,
				Y = (int)Top,
				Width = (int)Width,
				Height = (int)Height,
			});
		}

		#region Move and Resize

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
		#endregion

		#region HotKeys

		private bool _shiftHolding;
		private bool _ctrlHolding;
		public void HotKey(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.LeftShift || e.Key == Key.RightShift) {
				_shiftHolding = e.IsDown;
			}
			if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) {
				_ctrlHolding = e.IsDown;
			}

			if (e.IsDown) {
				switch (e.Key) {
					case Key.Up:
						if (_ctrlHolding && _shiftHolding) {
							Height--;
						} else if (_ctrlHolding && !_shiftHolding) {
							Top--;
							Height++;
						} else {
							Top -= _shiftHolding ? 10 : 1;
						}
						break;
					case Key.Down:
						if (_ctrlHolding && _shiftHolding) {
							Height++;
						} else if (_ctrlHolding && !_shiftHolding) {
							Top++;
							Height--;
						} else {
							Top += _shiftHolding ? 10 : 1;
						}
						break;
					case Key.Left:
						if (_ctrlHolding && _shiftHolding) {
							Width--;
						} else if (_ctrlHolding && !_shiftHolding) {
							Left--;
							Width++;
						} else {
							Left -= _shiftHolding ? 10 : 1;
						}
						break;
					case Key.Right:
						if (_ctrlHolding && _shiftHolding) {
							Width++;
						} else if (_ctrlHolding && !_shiftHolding) {
							Left++;
							Width--;
						} else {
							Left += _shiftHolding ? 10 : 1;
						}
						break;
				}
			}
		}

		#endregion

	}
}
