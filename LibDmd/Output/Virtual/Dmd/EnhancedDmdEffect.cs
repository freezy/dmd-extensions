using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LibDmd.Output.Virtual.Dmd
{
	/// <summary>
	/// An effect that transforms upscaled pixel into dots, applying a sort of HDR bloom and overlaying a glass on it.
	/// </summary>
	public class EnhancedDmdEffect : ShaderEffect
	{
		static EnhancedDmdEffect()
		{
			// Associate _pixelShader with our compiled pixel shader
			_pixelShader.UriSource = Global.MakePackUri("Output/Virtual/Dmd/EnhancedDmd.ps");
		}
		private static readonly PixelShader _pixelShader = new PixelShader();

		public EnhancedDmdEffect()
		{
			PixelShader = _pixelShader;
			UpdateShaderValue(DmdProperty);
			UpdateShaderValue(DmdLevel1Property);
			UpdateShaderValue(DmdLevel2Property);
			UpdateShaderValue(DmdLevel3Property);
			UpdateShaderValue(DmdLevel4Property);
			UpdateShaderValue(SizeProperty);
			UpdateShaderValue(GlassTexOffsetProperty);
			UpdateShaderValue(GlassTexScaleProperty);
			UpdateShaderValue(BackGlowProperty);
			UpdateShaderValue(BrightnessProperty);
			UpdateShaderValue(DotSizeProperty);
			UpdateShaderValue(GlassProperty);
			UpdateShaderValue(GlassColorProperty);
		}

		public Brush Dmd
		{
			get { return (Brush)GetValue(DmdProperty); }
			set { SetValue(DmdProperty, value); }
		}

		public static readonly DependencyProperty DmdProperty =
			RegisterPixelShaderSamplerProperty("Dmd", typeof(EnhancedDmdEffect), 0);

		public Brush DmdLevel1
		{
			get { return (Brush)GetValue(DmdLevel1Property); }
			set { SetValue(DmdLevel1Property, value); }
		}

		public static readonly DependencyProperty DmdLevel1Property =
			RegisterPixelShaderSamplerProperty("DmdLevel1", typeof(EnhancedDmdEffect), 1);

		public Brush DmdLevel2
		{
			get { return (Brush)GetValue(DmdLevel2Property); }
			set { SetValue(DmdLevel2Property, value); }
		}

		public static readonly DependencyProperty DmdLevel2Property =
			RegisterPixelShaderSamplerProperty("DmdLevel2", typeof(EnhancedDmdEffect), 2);

		public Brush DmdLevel3
		{
			get { return (Brush)GetValue(DmdLevel3Property); }
			set { SetValue(DmdLevel3Property, value); }
		}

		public static readonly DependencyProperty DmdLevel3Property =
			RegisterPixelShaderSamplerProperty("DmdLevel3", typeof(EnhancedDmdEffect), 3);

		public Brush DmdLevel4
		{
			get { return (Brush)GetValue(DmdLevel4Property); }
			set { SetValue(DmdLevel4Property, value); }
		}

		public static readonly DependencyProperty DmdLevel4Property =
			RegisterPixelShaderSamplerProperty("DmdLevel4", typeof(EnhancedDmdEffect), 4);

		public Brush Glass
		{
			get { return (Brush)GetValue(GlassProperty); }
			set { SetValue(GlassProperty, value); }
		}

		public static readonly DependencyProperty GlassProperty =
			RegisterPixelShaderSamplerProperty("Glass", typeof(EnhancedDmdEffect), 5);

		public Point Size
		{
			get { return (Point)GetValue(SizeProperty); }
			set { SetValue(SizeProperty, value); }
		}

		public static readonly DependencyProperty SizeProperty =
			DependencyProperty.Register("Size", typeof(Point), typeof(EnhancedDmdEffect),
			  new UIPropertyMetadata(new Point(128, 32), PixelShaderConstantCallback(0)));

		public Point GlassTexOffset
		{
			get { return (Point)GetValue(GlassTexOffsetProperty); }
			set { SetValue(GlassTexOffsetProperty, value); }
		}

		public static readonly DependencyProperty GlassTexOffsetProperty =
			DependencyProperty.Register("GlassTexOffset", typeof(Point), typeof(EnhancedDmdEffect),
			  new UIPropertyMetadata(new Point(0, 0), PixelShaderConstantCallback(1)));

		public Point GlassTexScale
		{
			get { return (Point)GetValue(GlassTexScaleProperty); }
			set { SetValue(GlassTexScaleProperty, value); }
		}

		public static readonly DependencyProperty GlassTexScaleProperty =
			DependencyProperty.Register("GlassTexScale", typeof(Point), typeof(EnhancedDmdEffect),
			  new UIPropertyMetadata(new Point(1, 1), PixelShaderConstantCallback(2)));

		public double BackGlow
		{
			get { return (double)GetValue(BackGlowProperty); }
			set { SetValue(BackGlowProperty, value); }
		}

		public static readonly DependencyProperty BackGlowProperty =
			DependencyProperty.Register("BackGlow", typeof(double), typeof(EnhancedDmdEffect),
			  new UIPropertyMetadata(0.0d, PixelShaderConstantCallback(3)));

		public double Brightness
		{
			get { return (double)GetValue(BrightnessProperty); }
			set { SetValue(BrightnessProperty, value); }
		}

		public static readonly DependencyProperty BrightnessProperty =
			DependencyProperty.Register("Brightness", typeof(double), typeof(EnhancedDmdEffect),
			  new UIPropertyMetadata(1.0d, PixelShaderConstantCallback(4)));

		public double DotSize
		{
			get { return (double)GetValue(DotSizeProperty); }
			set { SetValue(DotSizeProperty, value); }
		}

		public static readonly DependencyProperty DotSizeProperty =
			DependencyProperty.Register("DotSize", typeof(double), typeof(EnhancedDmdEffect),
			  new UIPropertyMetadata(0.8d, PixelShaderConstantCallback(5)));

		public double DotGlow
		{
			get { return (double)GetValue(DotGlowProperty); }
			set { SetValue(DotGlowProperty, value); }
		}

		public static readonly DependencyProperty DotGlowProperty =
			DependencyProperty.Register("DotGlow", typeof(double), typeof(EnhancedDmdEffect),
			  new UIPropertyMetadata(0.0d, PixelShaderConstantCallback(6)));

		public Color GlassColor
		{
			get { return (Color)GetValue(GlassColorProperty); }
			set { SetValue(GlassColorProperty, value); }
		}

		public static readonly DependencyProperty GlassColorProperty =
			DependencyProperty.Register("GlassColor", typeof(Color), typeof(EnhancedDmdEffect),
			  new UIPropertyMetadata(new Color(), PixelShaderConstantCallback(7)));
	}
}
