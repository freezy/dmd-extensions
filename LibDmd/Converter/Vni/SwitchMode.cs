namespace LibDmd.Converter.Vni
{
	public enum SwitchMode
	{
		/// <summary>
		/// Nii Palettä muäss gladä wärdä. Drbii isch dr `PaletteIndex`
		/// d Numärä vo dr Palettä uism Palettä-Feil. Wiä lang d Palettä
		/// gladä wird chunnt vo dr `Duration`.
		/// </summary>
		Palette = 0,

		/// <summary>
		/// Än Animazion uism FSQ-Feil wird abgschpiut. Weli genai definiärt
		/// d `Duration`. Drbii wird wiä im Modus 0 ai d Palettä gladä.
		/// </summary>
		Replace = 1,

		/// <summary>
		/// Aui Biudr wo chemid wärdid mit dä Zweibit-Datä uism FSQ-Feil
		/// erwiitered. D Idee isch dass uis Zwäibit-Datä Viärbit-Datä wärdid.
		/// D Palettä wird wiä obä ai gladä.
		/// </summary>
		ColorMask = 2,

		/// <summary>
		/// Wird nid bruichd.
		/// </summary>
		Event = 3,

		Follow = 4,

		LayeredColorMask = 5,

		FollowReplace = 6,

		MaskedReplace = 7
	}
}
