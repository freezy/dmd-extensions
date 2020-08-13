using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LibDmd.Output.Virtual.Dmd
{
	/// <summary>
	/// An effect that transforms upscaled pixel into dots, applying a sort of HDR bloom and overlaying a glass on it.
	/// </summary>
	public class BaseDmdEffect : ShaderEffect
	{
		static BaseDmdEffect()
		{
			// Associate _pixelShader with our compiled pixel shader
			_pixelShader.UriSource = Global.MakePackUri("Output/Virtual/Dmd/BaseDmd.ps");
		}
		private static readonly PixelShader _pixelShader = new PixelShader();

		public BaseDmdEffect()
		{
			PixelShader = _pixelShader;
			UpdateShaderValue(DmdProperty);
			UpdateShaderValue(SizeProperty);
			UpdateShaderValue(BrightnessProperty);
			UpdateShaderValue(DotSizeProperty);
		}

		public Brush Dmd
		{
			get { return (Brush)GetValue(DmdProperty); }
			set { SetValue(DmdProperty, value); }
		}

		public static readonly DependencyProperty DmdProperty =
			RegisterPixelShaderSamplerProperty("Dmd", typeof(BaseDmdEffect), 0);


		public Point Size
		{
			get { return (Point)GetValue(SizeProperty); }
			set { SetValue(SizeProperty, value); }
		}

		public static readonly DependencyProperty SizeProperty =
			DependencyProperty.Register("Size", typeof(Point), typeof(BaseDmdEffect),
			  new UIPropertyMetadata(new Point(128, 32), PixelShaderConstantCallback(0)));


		public double Brightness
		{
			get { return (double)GetValue(BrightnessProperty); }
			set { SetValue(BrightnessProperty, value); }
		}

		public static readonly DependencyProperty BrightnessProperty =
			DependencyProperty.Register("Brightness", typeof(double), typeof(BaseDmdEffect),
			  new UIPropertyMetadata(1.0d, PixelShaderConstantCallback(4)));

		public double DotSize
		{
			get { return (double)GetValue(DotSizeProperty); }
			set { SetValue(DotSizeProperty, value); }
		}

		public static readonly DependencyProperty DotSizeProperty =
			DependencyProperty.Register("DotSize", typeof(double), typeof(BaseDmdEffect),
			  new UIPropertyMetadata(0.8d, PixelShaderConstantCallback(5)));

	}
}
