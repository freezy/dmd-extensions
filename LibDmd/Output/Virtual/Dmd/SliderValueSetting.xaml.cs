using System;
using System.Globalization;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Controls;

namespace LibDmd.Output.Virtual.Dmd
{
	/// <summary>
	/// A control with a label, a slider and a text box
	/// </summary>
	public partial class SliderValueSetting : UserControl
	{
		public ISubject<double> OnValueChanged { get; } = new Subject<double>();

		public static readonly DependencyProperty LabelContentDependency = DependencyProperty.Register("LabelContent", typeof(string), typeof(SliderValueSetting));
		public static readonly DependencyProperty LabelWidthDependency = DependencyProperty.Register("LabelWidth", typeof(GridLength), typeof(SliderValueSetting));
		public static readonly DependencyProperty LabelAlignmentDependency = DependencyProperty.Register("LabelAlignment", typeof(HorizontalAlignment), typeof(SliderValueSetting));
		public static readonly DependencyProperty MinValueDependency = DependencyProperty.Register("MinValue", typeof(double), typeof(SliderValueSetting));
		public static readonly DependencyProperty MaxValueDependency = DependencyProperty.Register("MaxValue", typeof(double), typeof(SliderValueSetting));

		public double Value {
			get => SliderValue.Value;
			set {
				Update(value);
				OnValueChanged.OnNext(value);
			}
		}

		public void Update(double val) {
			SliderValue.Value = val;
			TextValue.Text = DoubleToString(val);
		}

		public string LabelContent {
			get => (string)GetValue(LabelContentDependency);
			set => SetValue(LabelContentDependency, value);
		}

		public GridLength LabelWidth {
			get => (GridLength)GetValue(LabelWidthDependency);
			set => SetValue(LabelWidthDependency, value);
		}
		
		public HorizontalAlignment LabelAlignment {
			get => (HorizontalAlignment)GetValue(LabelAlignmentDependency);
			set => SetValue(LabelAlignmentDependency, value);
		}

		public double MinValue {
			get => (double)GetValue(MinValueDependency);
			set => SetValue(MinValueDependency, value);
		}

		public double MaxValue {
			get => (double)GetValue(MaxValueDependency);
			set => SetValue(MaxValueDependency, value);
		}

		public SliderValueSetting()
		{
			InitializeComponent();

			LabelWidth = new GridLength(50);
			LabelAlignment = HorizontalAlignment.Left;

			SliderValue.ValueChanged += (sender, e) => TextValue.Text = DoubleToString(SliderValue.Value);
			TextValue.TextChanged += (sender, e) => SliderValue.Value = StringToDouble(TextValue.Text, SliderValue.Value);
			SliderValue.ValueChanged += (sender, e) => OnValueChanged.OnNext(SliderValue.Value);
		}

		private static string DoubleToString(double d)
		{
			return (Math.Round(d * 100) / 100).ToString("G", CultureInfo.InvariantCulture);
		}

		private static double StringToDouble(string str, double fallback)
		{
			try {
				return double.Parse(str);

			} catch (Exception) {
				return fallback;
			}
		}
	}
}
