using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LibDmd.Common;
using NLog;

namespace LibDmd.Converter.Vni
{
	public class VniLoader
	{
		public VniFile Vni;
		public PalFile Pal;
		public bool FilesExist => _pacPath != null || _palPath != null;

		private readonly string _palPath;
		private readonly string _vniPath;
		private readonly string _pacPath;
		private BinaryReader _reader;
		private const byte LatestPacVersion = 2;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		
		private enum DataType : ushort {
			Pal = 1,
			Vni = 2,
		}

		public VniLoader(string altColorPath, string gameName)
		{
			var altColorDir = new DirectoryInfo(Path.Combine(altColorPath, gameName));
			SetPath(PathUtil.GetLastCreatedFile(altColorDir, "pac"), ref _pacPath);
			SetPath(PathUtil.GetLastCreatedFile(altColorDir, "pal"), ref _palPath);
			SetPath(PathUtil.GetLastCreatedFile(altColorDir, "vni"), ref _vniPath);
		}

		public void Load(string vniKey)
		{
			if (_pacPath != null) {
				LoadPac(vniKey);
				return;
			}
			
			if (_palPath != null) {
				Logger.Info("[vni] Loading palette file at {0}...", _palPath);
				Pal = new PalFile(_palPath);
			}

			if (_vniPath != null) {
				Logger.Info("[vni] Loading virtual animation file at {0}...", _vniPath);
				Vni = new VniFile(_vniPath);
				Logger.Info("[vni] Loaded animation set {0}", Vni);
				Logger.Info("[vni] Animation Dimensions: {0}x{1}", Vni.Dimensions.Width, Vni.Dimensions.Height);
				Analytics.Instance.SetColorizer("VNI/PAL");
				
			} else {
				Logger.Info("[vni] No animation set found");
				Analytics.Instance.SetColorizer("PAL");
			}
		}

		private void LoadPac(string vniKey)
		{
			if (vniKey == null) {
				throw new ArgumentException("[vni] No PAC key found. Set it in DmdDevice.ini under vni.key.");
			}
			var key = HexToBytes(vniKey);
			Logger.Info("[vni] Loading PAC file at {0}...", _pacPath);
			using (_reader = new BinaryReader(File.OpenRead(_pacPath))) {
				var header = _reader.ReadBytes(4);
				if (Encoding.Default.GetString(header) != "PAC ") {
					throw new WrongFormatException($"Cannot read {_pacPath}, doesn't seem to be a valid PAC file.");
				}
				var version = _reader.ReadByte();
				if (version > LatestPacVersion) {
					throw new WrongFormatException($"Cannot read {_pacPath}, PAC v{version} is not supported.");
				}

				NextChunk(key, version);
				var bs = _reader.BaseStream;
				if (bs.Position == bs.Length) { // EOF?
					Logger.Info($"[vni] PAC v{version} without animations loaded successfully.");
					return;
				}
				NextChunk(key, version);
				Logger.Info($"[vni] PAC v{version} loaded successfully.");
				Analytics.Instance.SetColorizer("PAC");
			}
		}

		private void NextChunk(byte[] key, byte version)
		{
			try {
				var type = (DataType)Enum.ToObject(typeof(DataType), _reader.ReadInt16BE());
				var len = _reader.ReadInt32BE();
				var encryptedData = _reader.ReadBytesRequired(len);
				if (version == 2) {
					DecodePacV2(encryptedData);
				}
				var data = Decompress(Decrypt(encryptedData, key));
				switch (type) {
					case DataType.Pal:
						Pal = new PalFile(data, Path.GetFileName(_pacPath));
						break;
					case DataType.Vni:
						Vni = new VniFile(data, Path.GetFileName(_pacPath));
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			} catch (CryptographicException e) {
				Logger.Error($"[vni] Could not decrypt PAC file: {e.Message}");
			}
		}

		/// <summary>
		/// PAC v2 reverses all 32 bits in every encrypted four-byte word. The
		/// operation is its own inverse, so applying it restores the PAC v1 AES
		/// ciphertext before decryption.
		/// </summary>
		private static void DecodePacV2(byte[] data)
		{
			if (data.Length % 4 != 0) {
				throw new InvalidDataException("PAC v2 encrypted chunk length must be a multiple of four bytes.");
			}

			for (var i = 0; i < data.Length; i += 4) {
				var byte0 = ReverseBits(data[i + 3]);
				var byte1 = ReverseBits(data[i + 2]);
				var byte2 = ReverseBits(data[i + 1]);
				var byte3 = ReverseBits(data[i]);
				data[i] = byte0;
				data[i + 1] = byte1;
				data[i + 2] = byte2;
				data[i + 3] = byte3;
			}
		}

		private static byte ReverseBits(byte value)
		{
			value = (byte)(((value & 0xf0) >> 4) | ((value & 0x0f) << 4));
			value = (byte)(((value & 0xcc) >> 2) | ((value & 0x33) << 2));
			return (byte)(((value & 0xaa) >> 1) | ((value & 0x55) << 1));
		}

		private static byte[] Decompress(byte[] input)
		{
			using (var stream = new GZipStream(new MemoryStream(input), CompressionMode.Decompress))
			using (var ms = new MemoryStream()) {
				stream.CopyTo(ms);
				return ms.ToArray();
			}
		}

		private static byte[] Decrypt(byte[] data, byte[] key)
		{
			using (var aes = Aes.Create()) {
				aes.KeySize = 128;
				aes.BlockSize = 128;
				aes.Padding = PaddingMode.Zeros;
				aes.Key = key;
				aes.IV = key;
				using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV)) {
					return Decrypt(data, decryptor);
				}
			}
		}
		
		private static byte[] Decrypt(byte[] data, ICryptoTransform cryptoTransform)
		{
			using (var ms = new MemoryStream())
			using (var cryptoStream = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write)) {
				cryptoStream.Write(data, 0, data.Length);
				cryptoStream.FlushFinalBlock();
				return ms.ToArray();
			}
		}

		private static byte[] HexToBytes(string hex)
		{
			hex = hex.Trim();
			if (hex.Length % 2 == 1) {
				throw new Exception("The binary key cannot have an odd number of digits");
			}
			byte[] arr = new byte[hex.Length >> 1];
			for (int i = 0; i < hex.Length >> 1; ++i) {
				arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
			}
			return arr;
		}

		private static int GetHexVal(char hex) {
			int val = hex;
			return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
		}

		private static void SetPath(FileSystemInfo fi, ref string dest)
		{
			if (fi == null) {
				return;
			}
			var path = fi.FullName;
			if (!File.Exists(path)) {
				return;
			}

			dest = path;
		}
	}
}
