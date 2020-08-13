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
			set
			{
				_padding = value;
				LeftSlider.Value = _padding.Left;
				RightSlider.Value = _padding.Right;
				TopSlider.Value = _padding.Top;
				BottomSlider.Value = _padding.Bottom;
				LeftValue.Text = DoubleToString(LeftSlider.Value);
				RightValue.Text = DoubleToString(RightSlider.Value);
				TopValue.Text = DoubleToString(TopSlider.Value);
				BottomValue.Text = DoubleToString(BottomSlider.Value);
			}
		}

		private Thickness _padding;

		public DmdPaddingSetting()
		{
			InitializeComponent();

			LeftSlider.ValueChanged += (sender, e) => LeftValue.Text = DoubleToString(LeftSlider.Value);
			LeftValue.TextChanged += (sender, e) => LeftSlider.Value = StringToDouble(LeftValue.Text, LeftSlider.Value);
			LeftSlider.ValueChanged += (sender, e) => _padding.Left = LeftSlider.Value;
			LeftSlider.ValueChanged += (sender, e) => OnPaddingChanged.OnNext(_padding);

			RightSlider.ValueChanged += (sender, e) => RightValue.Text = DoubleToString(RightSlider.Value);
			RightValue.TextChanged += (sender, e) => RightSlider.Value = StringToDouble(RightValue.Text, RightSlider.Value);
			RightSlider.ValueChanged += (sender, e) => _padding.Right = RightSlider.Value;
			RightSlider.ValueChanged += (sender, e) => OnPaddingChanged.OnNext(_padding);

			TopSlider.ValueChanged += (sender, e) => TopValue.Text = DoubleToString(TopSlider.Value);
			TopValue.TextChanged += (sender, e) => TopSlider.Value = StringToDouble(TopValue.Text, TopSlider.Value);
			TopSlider.ValueChanged += (sender, e) => _padding.Top = TopSlider.Value;
			TopSlider.ValueChanged += (sender, e) => OnPaddingChanged.OnNext(_padding);

			BottomSlider.ValueChanged += (sender, e) => BottomValue.Text = DoubleToString(BottomSlider.Value);
			BottomValue.TextChanged += (sender, e) => BottomSlider.Value = StringToDouble(BottomValue.Text, BottomSlider.Value);
			BottomSlider.ValueChanged += (sender, e) => _padding.Bottom = BottomSlider.Value;
			BottomSlider.ValueChanged += (sender, e) => OnPaddingChanged.OnNext(_padding);
		}

		private static string DoubleToString(double d)
		{
			return ((int)Math.Round(d)).ToString();
		}

		private static double StringToDouble(string str, double fallback)
		{
			try
			{
				return double.Parse(str);
			}
			catch (Exception)
			{
				return fallback;
			}
		}
	}
}
