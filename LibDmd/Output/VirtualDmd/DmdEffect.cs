using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LibDmd.Output.VirtualDmd
{
	/// <summary>
	/// An effect that transforms upscaled pixel into dots.
	/// </summary>
	/// <see cref="http://stackoverflow.com/questions/25452349/wpf-shader-effect-antialiasing-not-showing"/>
	public class DmdEffect : ShaderEffect
	{
		static DmdEffect()
		{
			// Associate _pixelShader with our compiled pixel shader
			_pixelShader.UriSource = Global.MakePackUri("Output/VirtualDmd/Dmd.ps");
		}
		private static readonly PixelShader _pixelShader = new PixelShader();

		public DmdEffect()
		{
			PixelShader = _pixelShader;
			UpdateShaderValue(InputProperty);
			UpdateShaderValue(BlockCountProperty);
			UpdateShaderValue(MaxProperty);
			UpdateShaderValue(AspectRatioProperty);
			UpdateShaderValue(FilterColorProperty);
			UpdateShaderValue(IsMonochromeProperty);
		}

		public Brush Input
		{
			get { return (Brush)GetValue(InputProperty); }
			set { SetValue(InputProperty, value); }
		}

		public static readonly DependencyProperty InputProperty =
			RegisterPixelShaderSamplerProperty("Input", typeof(DmdEffect), 0);

		public double BlockCount
		{
			get { return (double)GetValue(BlockCountProperty); }
			set { SetValue(BlockCountProperty, value); }
		}

		public static readonly DependencyProperty BlockCountProperty =
			DependencyProperty.Register("BlockCount", typeof(double), typeof(DmdEffect),
			  new UIPropertyMetadata(1.0d, PixelShaderConstantCallback(0)));

		public double Max
		{
			get { return (double)GetValue(MaxProperty); }
			set { SetValue(MaxProperty, value); }
		}

		public static readonly DependencyProperty MaxProperty =
			DependencyProperty.Register("Max", typeof(double), typeof(DmdEffect),
			  new UIPropertyMetadata(1.0d, PixelShaderConstantCallback(2)));

		public double AspectRatio
		{
			get { return (double)GetValue(AspectRatioProperty); }
			set { SetValue(AspectRatioProperty, value); }
		}

		public static readonly DependencyProperty AspectRatioProperty =
			DependencyProperty.Register("AspectRatio", typeof(double), typeof(DmdEffect),
			  new UIPropertyMetadata(1.0d, PixelShaderConstantCallback(3)));

		public Color FilterColor
		{
			get { return (Color)GetValue(FilterColorProperty); }
			set { SetValue(FilterColorProperty, value); }
		}

		public static readonly DependencyProperty FilterColorProperty =
			DependencyProperty.Register("FilterColor", typeof(Color), typeof(DmdEffect),
			  new UIPropertyMetadata(new Color(), PixelShaderConstantCallback(1)));

		public double IsMonochrome
		{
			get { return (double)GetValue(IsMonochromeProperty); }
			set { SetValue(IsMonochromeProperty, value); }
		}

		public static readonly DependencyProperty IsMonochromeProperty =
			DependencyProperty.Register("IsMonochrome", typeof(double), typeof(DmdEffect),
			  new UIPropertyMetadata(0.0d, PixelShaderConstantCallback(4)));
	}

	internal static class Global
	{
		public static Uri MakePackUri(string relativeFile)
		{
			var uriString = "pack://application:,,,/" + AssemblyShortName + ";component/" + relativeFile;
			return new Uri(uriString);
		}

		private static string AssemblyShortName
		{
			get
			{
				if (_assemblyShortName == null) {
					var a = typeof(Global).Assembly;
					_assemblyShortName = a.ToString().Split(',')[0];
				}
				return _assemblyShortName;
			}
		}

		private static string _assemblyShortName;
	}
}
