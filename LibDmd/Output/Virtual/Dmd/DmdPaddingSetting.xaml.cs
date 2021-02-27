using System;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Controls;

namespace LibDmd.Output.Virtual.Dmd
{
	/// <summary>
	/// Interaction logic for DmdPaddingSetting.xaml
	/// </summary>
	public partial class DmdPaddingSetting : UserControl
	{
		public ISubject<Thickness> OnPaddingChanged { get; } = new Subject<Thickness>();

		public Thickness Pad
		{
			get => _padding;
			set {
				_padding = value;
				PaddingLeft.Value = _padding.Left;
				PaddingTop.Value = _padding.Top;
				PaddingRight.Value = _padding.Right;
				PaddingBottom.Value = _padding.Bottom;
			}
		}

		private Thickness _padding;

		public DmdPaddingSetting()
		{
			InitializeComponent();

			PaddingLeft.OnValueChanged.Subscribe((val) => {
				_padding.Left = val;
				ApplyLock(val);
				OnPaddingChanged.OnNext(_padding);
			});

			PaddingTop.OnValueChanged.Subscribe((val) => {
				_padding.Top = val;
				ApplyLock(val);
				OnPaddingChanged.OnNext(_padding);
			});

			PaddingRight.OnValueChanged.Subscribe((val) => {
				_padding.Right = val;
				ApplyLock(val);
				OnPaddingChanged.OnNext(_padding);
			});

			PaddingBottom.OnValueChanged.Subscribe((val) => {
				_padding.Bottom = val;
				ApplyLock(val);
				OnPaddingChanged.OnNext(_padding);
			});
		}

		private void ApplyLock(double val) {
			if ((bool)Locked.IsChecked) {
				_padding.Left = val;
				_padding.Top = val;
				_padding.Right = val;
				_padding.Bottom = val;

				PaddingLeft.Update(val);
				PaddingTop.Update(val);
				PaddingRight.Update(val);
				PaddingBottom.Update(val);
			}
		}
	}
}
