using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PinMameDevice
{
	public class AlphaNumeric
	{

		static readonly byte[] frame_buf = new byte[3072];
		static bool do16 = false;

		static byte[,] segSizes = {
			{5,5,5,5,5,5,2,2,5,5,5,2,5,5,5,1},
			{5,5,5,5,5,5,5,2,0,0,0,0,0,0,0,0},
			{5,5,5,5,5,5,5,2,5,5,0,0,0,0,0,0},
			{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},
			{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},
			{5,2,2,5,2,2,5,2,0,0,0,0,0,0,0,0},
			{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},
		};

		static byte[,,,] segs = {	

			// Alphanumeric display characters
			{
				{{1,0},{2,0},{3,0},{4,0},{5,0}},
				{{6,0},{6,1},{6,2},{6,3},{6,4}},
				{{6,6},{6,7},{6,8},{6,9},{6,10}},
				{{1,10},{2,10},{3,10},{4,10},{5,10}},
				{{0,6},{0,7},{0,8},{0,9},{0,10}},
				{{0,0},{0,1},{0,2},{0,3},{0,4}},
				{{1,5},{2,5},{0,0},{0,0},{0,0}},
				{{7,9},{7,10},{0,0},{0,0},{0,0}},
				{{0,0},{1,1},{1,2},{2,3},{2,4}},
				{{3,0},{3,1},{3,2},{3,3},{3,4}},
				{{6,0},{5,1},{5,2},{4,3},{4,4}},
				{{4,5},{5,5},{0,0},{0,0},{0,0}},
				{{4,6},{4,7},{5,8},{5,9},{6,10}},
				{{3,6},{3,7},{3,8},{3,9},{3,10}},
				{{0,10},{2,6},{2,7},{1,8},{1,9}},
				{{7,10},{0,0},{0,0},{0,0},{0,0}}
			},

			// 8 segment LED characters
			{
				{{1,0},{2,0},{3,0},{4,0},{5,0}},
				{{6,0},{6,1},{6,2},{6,3},{6,4}},
				{{6,6},{6,7},{6,8},{6,9},{6,10}},
				{{1,10},{2,10},{3,10},{4,10},{5,10}},
				{{0,6},{0,7},{0,8},{0,9},{0,10}},
				{{0,0},{0,1},{0,2},{0,3},{0,4}},
				{{1,5},{2,5},{3,5},{4,5},{5,5}},
				{{7,9},{7,10},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}}
			},
			// 10 segment LED characters
			{
				{{1,0},{2,0},{3,0},{4,0},{5,0}},
				{{6,0},{6,1},{6,2},{6,3},{6,4}},
				{{6,6},{6,7},{6,8},{6,9},{6,10}},
				{{1,10},{2,10},{3,10},{4,10},{5,10}},
				{{0,6},{0,7},{0,8},{0,9},{0,10}},
				{{0,0},{0,1},{0,2},{0,3},{0,4}},
				{{1,5},{2,5},{3,5},{4,5},{5,5}},
				{{7,9},{7,10},{0,0},{0,0},{0,0}},
				{{3,0},{3,1},{3,2},{3,3},{3,4}},
				{{3,6},{3,7},{3,8},{3,9},{3,10}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}}
			},
			// alphanumeric display characters (reversed comma with period)
			{
				{{1,0},{2,0},{3,0},{4,0},{5,0}},
				{{6,0},{6,1},{6,2},{6,3},{6,4}},
				{{6,6},{6,7},{6,8},{6,9},{6,10}},
				{{1,10},{2,10},{3,10},{4,10},{5,10}},
				{{0,6},{0,7},{0,8},{0,9},{0,10}},
				{{0,0},{0,1},{0,2},{0,3},{0,4}},
				{{1,5},{2,5},{0,0},{0,0},{0,0}},
				{{7,9},{7,10},{0,0},{0,0},{0,0}},
				{{0,0},{1,1},{1,2},{2,3},{2,4}},
				{{3,0},{3,1},{3,2},{3,3},{3,4}},
				{{6,0},{5,1},{5,2},{4,3},{4,4}},
				{{4,5},{5,5},{0,0},{0,0},{0,0}},
				{{4,6},{4,7},{5,8},{5,9},{6,10}},
				{{3,6},{3,7},{3,8},{3,9},{3,10}},
				{{0,10},{2,6},{2,7},{1,8},{1,9}},
				{{7,10},{0,0},{0,0},{0,0},{0,0}}
			}, 
			// 8 segment LED characters with dots instead of commas
			{
				{{1,0},{2,0},{3,0},{4,0},{5,0}},
				{{6,0},{6,1},{6,2},{6,3},{6,4}},
				{{6,6},{6,7},{6,8},{6,9},{6,10}},
				{{1,10},{2,10},{3,10},{4,10},{5,10}},
				{{0,6},{0,7},{0,8},{0,9},{0,10}},
				{{0,0},{0,1},{0,2},{0,3},{0,4}},
				{{1,5},{2,5},{3,5},{4,5},{5,5}},
				{{7,9},{7,10},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}}
			},

			// 8 segment LED characters SMALL
			{
				{{1,0},{2,0},{3,0},{4,0},{5,0}},
				{{6,1},{6,2},{0,0},{0,0},{0,0}},
				{{6,4},{6,5},{0,0},{0,0},{0,0}},
				{{1,6},{2,6},{3,6},{4,6},{5,6}},
				{{0,4},{0,5},{0,0},{0,0},{0,0}},
				{{0,1},{0,2},{0,0},{0,0},{0,0}},
				{{1,3},{2,3},{3,3},{4,3},{5,3}},
				{{7,5},{7,6},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}}
			},
			// 10 segment LED characters SMALL
			{
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}},
				{{0,0},{0,0},{0,0},{0,0},{0,0}}
			}
		};


		static void smoothDigitCorners(int x, int y)
		{
			if (getPixel(x, 1 + y) && getPixel(1 + x, y))
				drawPixel(0 + x, y, 0);
			if (getPixel(x + 6, 1 + y) && getPixel(5 + x, y))
				drawPixel(6 + x, y, 0);
			if (getPixel(x, 9 + y) && getPixel(1 + x, 10 + y))
				drawPixel(0 + x, 10 + y, 0);
			if (getPixel(x + 6, 9 + y) && getPixel(5 + x, 10 + y))
				drawPixel(6 + x, 10 + y, 0);
		}

		static void drawSegment(int x, int y, byte type, byte seg, byte colour)
		{
			int i;
			for (i = 0; i < segSizes[type, seg]; i++) {
				drawPixel(segs[type, seg, i, 0] + x, segs[type, seg, i, 1] + y, colour);
			}
		}

		static bool getPixel(int x, int y)
		{
			int v, z;
			v = (y * 16) + (x / 8);
			z = 1 << (x % 8);
			// just check high buff
			return ((frame_buf[v + 512 + 4] & z) != 0);
		}

		static void drawPixel(int x, int y, byte colour)
		{
			byte v, z;
			v = (byte)((y * 16) + (x / 8));
			z = (byte)(1 << (x % 8));
			// clear both low and high buffer pixel
			frame_buf[v + 4] |= z;
			frame_buf[v + 512 + 4] |= z;
			frame_buf[v + 4] ^= z;
			frame_buf[v + 512 + 4] ^= z;
			if (do16) {
				frame_buf[v + 1024 + 4] |= z;
				frame_buf[v + 1536 + 4] |= z;
				frame_buf[v + 1024 + 4] ^= z;
				frame_buf[v + 1536 + 4] ^= z;
			}
			// set low buffer pixel
			if ((colour & 1) > 0)
				frame_buf[v + 4] |= z;
			//set high buffer pixel
			if ((colour & 2) > 0)
				frame_buf[v + 512 + 4] ^= z;
			// 16 colour mode
			if (do16) {
				if (colour != 0) {
					frame_buf[v + 1024 + 4] |= z;
					frame_buf[v + 1536 + 4] ^= z;
				}
			}
		}






		static void smoothDigitCorners(byte[] buffer, int x, int y)
		{
			if (getPixel(buffer, x, 1 + y) && getPixel(buffer, 1 + x, y))
				drawPixel(buffer, 0 + x, y, 0);
			if (getPixel(buffer, x + 6, 1 + y) && getPixel(buffer, 5 + x, y))
				drawPixel(buffer, 6 + x, y, 0);
			if (getPixel(buffer, x, 9 + y) && getPixel(buffer, 1 + x, 10 + y))
				drawPixel(buffer, 0 + x, 10 + y, 0);
			if (getPixel(buffer, x + 6, 9 + y) && getPixel(buffer, 5 + x, 10 + y))
				drawPixel(buffer, 6 + x, 10 + y, 0);
		}

		static void drawSegment(byte[] buffer, int x, int y, byte type, byte seg, byte colour)
		{
			int i;
			for (i = 0; i < segSizes[type, seg]; i++) {
				drawPixel(buffer, segs[type, seg, i, 0] + x, segs[type, seg, i, 1] + y, colour);
			}
		}

		static bool getPixel(byte[] buffer, int x, int y)
		{
			int v, z;
			v = (y * 16) + (x / 8);
			z = 1 << (x % 8);
			// just check high buff
			return ((buffer[v + 512 + 4] & z) != 0);
		}

		static void drawPixel(byte[] buffer, int x, int y, byte colour)
		{
			byte v, z;
			v = (byte)(((byte)y * 16) + ((byte)x / 8));
			z = (byte)(1 << ((byte)x % 8));
			// clear both low and high buffer pixel
			buffer[v + 4] |= z;
			buffer[v + 512 + 4] |= z;
			buffer[v + 4] ^= z;
			buffer[v + 512 + 4] ^= z;
			if (do16) {
				buffer[v + 1024 + 4] |= z;
				buffer[v + 1536 + 4] |= z;
				buffer[v + 1024 + 4] ^= z;
				buffer[v + 1536 + 4] ^= z;
			}
			// set low buffer pixel
			if ((colour & 1) > 0)
				buffer[v + 4] |= z;
			//set high buffer pixel
			if ((colour & 2) > 0)
				buffer[v + 512 + 4] ^= z;
			// 16 colour mode
			if (do16) {
				if (colour != 0) {
					buffer[v + 1024 + 4] |= z;
					buffer[v + 1536 + 4] ^= z;
				}
			}
		}

		void _2x16Alpha(byte[] seg_data)
		{
			byte i, j;
			for (i = 0; i < 16; i++) {
				for (j = 0; j < 16; j++) {
					if (((seg_data[i] >> j) & 0x1) != 0)
						drawSegment(i * 8, 2, 0, j, 3);
					if (((seg_data[i + 16] >> j) & 0x1) != 0)
						drawSegment(i * 8, 19, 0, j, 3);
					smoothDigitCorners(i * 8, 2);
					smoothDigitCorners(i * 8, 19);
				}
			}
		}

		void _2x20Alpha(byte[] seg_data)
		{
			byte i, j;
			for (i = 0; i < 16; i++) {
				for (j = 0; j < 16; j++) {
					if (((seg_data[i] >> j) & 0x1) != 0)
						drawSegment(i * 8, 2, 0, j, 3);
					if (((seg_data[i + 20] >> j) & 0x1) != 0)
						drawSegment(i * 8, 19, 0, j, 3);
					smoothDigitCorners(i * 8, 2);
					smoothDigitCorners(i * 8, 19);
				}
			}
		}

		void _2x7Alpha_2x7Num(byte[] seg_data)
		{
			byte i, j;
			for (i = 0; i < 14; i++) {
				for (j = 0; j < 16; j++) {
					// 2x7 alphanumeric
					if (((seg_data[i] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 7) ? 0 : 2)) * 8, 2, 0, j, 3);
					smoothDigitCorners((i + ((i < 7) ? 0 : 2)) * 8, 2);
					// 2x7 numeric
					if (((seg_data[i + 14] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 7) ? 0 : 2)) * 8, 19, 1, j, 3);
					smoothDigitCorners((i + ((i < 7) ? 0 : 2)) * 8, 19);
				}
			}
		}

		void _2x7Alpha_2x7Num_4x1Num(byte[] seg_data)
		{
			byte i, j;
			for (i = 0; i < 14; i++) {
				for (j = 0; j < 16; j++) {
					// 2x7 alphanumeric
					if (((seg_data[i] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 7) ? 0 : 2)) * 8, 0, 0, j, 3);
					smoothDigitCorners((i + ((i < 7) ? 0 : 2)) * 8, 0);
					// 2x7 numeric
					if (((seg_data[i + 14] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 7) ? 0 : 2)) * 8, 21, 1, j, 3);
					smoothDigitCorners((i + ((i < 7) ? 0 : 2)) * 8, 21);
				}
			}
			// 4x1 numeric small
			for (j = 0; j < 16; j++) {
				if (((seg_data[28] >> j) & 0x1) != 0)
					drawSegment(8, 12, 5, j, 3);
				if (((seg_data[29] >> j) & 0x1) != 0)
					drawSegment(16, 12, 5, j, 3);
				if (((seg_data[30] >> j) & 0x1) != 0)
					drawSegment(32, 12, 5, j, 3);
				if (((seg_data[31] >> j) & 0x1) != 0)
					drawSegment(40, 12, 5, j, 3);
			}
		}

		void _2x6Num_2x6Num_4x1Num(byte[] seg_data)
		{
			byte i, j;
			for (i = 0; i < 12; i++) {
				for (j = 0; j < 16; j++) {
					// 2x7 numeric
					if (((seg_data[i] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 6) ? 0 : 4)) * 8, 0, 1, j, 3);
					smoothDigitCorners((i + ((i < 6) ? 0 : 4)) * 8, 0);
					// 2x7 numeric
					if (((seg_data[i + 12] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 6) ? 0 : 4)) * 8, 12, 1, j, 3);
					smoothDigitCorners((i + ((i < 6) ? 0 : 4)) * 8, 12);
				}
			}
			// 4x1 numeric small
			for (j = 0; j < 16; j++) {
				if (((seg_data[24] >> j) & 0x1) != 0)
					drawSegment(8, 24, 5, j, 3);
				if (((seg_data[25] >> j) & 0x1) != 0)
					drawSegment(16, 24, 5, j, 3);
				if (((seg_data[26] >> j) & 0x1) != 0)
					drawSegment(32, 24, 5, j, 3);
				if (((seg_data[27] >> j) & 0x1) != 0)
					drawSegment(40, 24, 5, j, 3);
			}
		}

		void _2x6Num10_2x6Num10_4x1Num(byte[] seg_data)
		{
			byte i, j;
			for (i = 0; i < 12; i++) {
				for (j = 0; j < 16; j++) {
					// 2x7 numeric
					if (((seg_data[i] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 6) ? 0 : 4)) * 8, 0, 2, j, 3);
					smoothDigitCorners((i + ((i < 6) ? 0 : 4)) * 8, 0);
					// 2x7 numeric
					if (((seg_data[i + 12] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 6) ? 0 : 4)) * 8, 20, 2, j, 3);
					smoothDigitCorners((i + ((i < 6) ? 0 : 4)) * 8, 20);
				}
			}
			// 4x1 numeric small
			for (j = 0; j < 16; j++) {
				if (((seg_data[24] >> j) & 0x1) != 0)
					drawSegment(8, 12, 5, j, 3);
				if (((seg_data[25] >> j) & 0x1) != 0)
					drawSegment(16, 12, 5, j, 3);
				if (((seg_data[26] >> j) & 0x1) != 0)
					drawSegment(32, 12, 5, j, 3);
				if (((seg_data[27] >> j) & 0x1) != 0)
					drawSegment(40, 12, 5, j, 3);
			}
		}

		void _2x7Num_2x7Num_4x1Num(byte[] seg_data)
		{
			byte i, j;
			for (i = 0; i < 14; i++) {
				for (j = 0; j < 16; j++) {
					// 2x7 numeric
					if (((seg_data[i] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 7) ? 0 : 2)) * 8, 0, 1, j, 3);
					smoothDigitCorners((i + ((i < 7) ? 0 : 2)) * 8, 0);
					// 2x7 numeric
					if (((seg_data[i + 14] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 7) ? 0 : 2)) * 8, 12, 1, j, 3);
					smoothDigitCorners((i + ((i < 7) ? 0 : 2)) * 8, 12);
				}
			}
			// 4x1 numeric small
			for (j = 0; j < 16; j++) {
				if (((seg_data[28] >> j) & 0x1) != 0)
					drawSegment(16, 24, 5, j, 3);
				if (((seg_data[29] >> j) & 0x1) != 0)
					drawSegment(24, 24, 5, j, 3);
				if (((seg_data[30] >> j) & 0x1) != 0)
					drawSegment(40, 24, 5, j, 3);
				if (((seg_data[31] >> j) & 0x1) != 0)
					drawSegment(48, 24, 5, j, 3);
			}
		}

		void _2x7Num_2x7Num_10x1Num(byte[] seg_data, byte[] extra_seg_data)
		{
			byte i, j;
			for (i = 0; i < 14; i++) {
				for (j = 0; j < 16; j++) {
					// 2x7 numeric
					if (((seg_data[i] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 7) ? 0 : 2)) * 8, 0, 1, j, 3);
					smoothDigitCorners((i + ((i < 7) ? 0 : 2)) * 8, 0);
					// 2x7 numeric
					if (((seg_data[i + 14] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 7) ? 0 : 2)) * 8, 12, 1, j, 3);
					smoothDigitCorners((i + ((i < 7) ? 0 : 2)) * 8, 12);
				}
			}
			// 10x1 numeric small
			for (j = 0; j < 16; j++) {
				if (((seg_data[28] >> j) & 0x1) != 0)
					drawSegment(16, 24, 5, j, 3);
				if (((seg_data[29] >> j) & 0x1) != 0)
					drawSegment(24, 24, 5, j, 3);
				if (((seg_data[30] >> j) & 0x1) != 0)
					drawSegment(40, 24, 5, j, 3);
				if (((seg_data[31] >> j) & 0x1) != 0)
					drawSegment(48, 24, 5, j, 3);
				if (((extra_seg_data[0] >> j) & 0x1) != 0)
					drawSegment(64, 24, 5, j, 3);
				if (((extra_seg_data[1] >> j) & 0x1) != 0)
					drawSegment(72, 24, 5, j, 3);
				if (((extra_seg_data[2] >> j) & 0x1) != 0)
					drawSegment(88, 24, 5, j, 3);
				if (((extra_seg_data[3] >> j) & 0x1) != 0)
					drawSegment(96, 24, 5, j, 3);
				if (((extra_seg_data[4] >> j) & 0x1) != 0)
					drawSegment(112, 24, 5, j, 3);
				if (((extra_seg_data[5] >> j) & 0x1) != 0)
					drawSegment(120, 24, 5, j, 3);
			}
		}

		void _2x7Num_2x7Num_4x1Num_gen7(byte[] seg_data)
		{
			byte i, j;
			for (i = 0; i < 14; i++) {
				for (j = 0; j < 16; j++) {
					// 2x7 numeric
					if (((seg_data[i] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 7) ? 0 : 2)) * 8, 21, 1, j, 3);
					smoothDigitCorners((i + ((i < 7) ? 0 : 2)) * 8, 21);
					// 2x7 numeric
					if (((seg_data[i + 14] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 7) ? 0 : 2)) * 8, 1, 1, j, 3);
					smoothDigitCorners((i + ((i < 7) ? 0 : 2)) * 8, 1);
				}
			}
			// 4x1 numeric small
			for (j = 0; j < 16; j++) {
				if (((seg_data[28] >> j) & 0x1) != 0)
					drawSegment(8, 13, 5, j, 3);
				if (((seg_data[29] >> j) & 0x1) != 0)
					drawSegment(16, 13, 5, j, 3);
				if (((seg_data[30] >> j) & 0x1) != 0)
					drawSegment(32, 13, 5, j, 3);
				if (((seg_data[31] >> j) & 0x1) != 0)
					drawSegment(40, 13, 5, j, 3);
			}
		}

		public static byte[] _2x7Num10_2x7Num10_4x1Num(byte[] seg_data)
		{
			byte i, j;
			var buffer = new byte[1028];
			for (i = 0; i < 14; i++) {
				for (j = 0; j < 16; j++) {
					// 2x7 numeric
					if (((seg_data[i] >> j) & 0x1) > 0)
						drawSegment(buffer, (i + ((i < 7) ? 0 : 2)) * 8, 0, 2, j, 3);
					smoothDigitCorners(buffer, (i + ((i < 7) ? 0 : 2)) * 8, 0);
					// 2x7 numeric
					if (((seg_data[i + 14] >> j) & 0x1) > 0)
						drawSegment(buffer, (i + ((i < 7) ? 0 : 2)) * 8, 20, 2, j, 3);
					smoothDigitCorners(buffer, (i + ((i < 7) ? 0 : 2)) * 8, 20);
				}
			}
			// 4x1 numeric small
			for (j = 0; j < 16; j++) {
				if (((seg_data[28] >> j) & 0x1) > 0)
					drawSegment(buffer, 8, 12, 5, j, 3);
				if (((seg_data[29] >> j) & 0x1) > 0)
					drawSegment(buffer, 16, 12, 5, j, 3);
				if (((seg_data[30] >> j) & 0x1) > 0)
					drawSegment(buffer, 32, 12, 5, j, 3);
				if (((seg_data[31] >> j) & 0x1) > 0)
					drawSegment(buffer, 40, 12, 5, j, 3);
			}
			return buffer;
		}

		void _4x7Num10(byte[] seg_data)
		{
			byte i, j;
			for (i = 0; i < 14; i++) {
				for (j = 0; j < 16; j++) {
					// 2x7 numeric10
					if (((seg_data[i] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 7) ? 0 : 2)) * 8, 1, 2, j, 3);
					smoothDigitCorners((i + ((i < 7) ? 0 : 2)) * 8, 1);
					// 2x7 numeric10
					if (((seg_data[i + 14] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 7) ? 0 : 2)) * 8, 13, 2, j, 3);
					smoothDigitCorners((i + ((i < 7) ? 0 : 2)) * 8, 13);
				}
			}
		}

		void _6x4Num_4x1Num(byte[] seg_data)
		{
			byte i, j;
			for (i = 0; i < 8; i++) {
				for (j = 0; j < 16; j++) {
					// 2x4 numeric
					if (((seg_data[i] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 4) ? 0 : 2)) * 8, 1, 5, j, 3);
					smoothDigitCorners((i + ((i < 4) ? 0 : 2)) * 8, 1);
					// 2x4 numeric
					if (((seg_data[i + 8] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 4) ? 0 : 2)) * 8, 9, 5, j, 3);
					smoothDigitCorners((i + ((i < 4) ? 0 : 2)) * 8, 1);
					// 2x4 numeric
					if (((seg_data[i + 16] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 4) ? 0 : 2)) * 8, 17, 5, j, 3);
					smoothDigitCorners((i + ((i < 4) ? 0 : 2)) * 8, 1);
				}
			}
			// 4x1 numeric small
			for (j = 0; j < 16; j++) {
				if (((seg_data[24] >> j) & 0x1) != 0)
					drawSegment(16, 25, 5, j, 3);
				if (((seg_data[25] >> j) & 0x1) != 0)
					drawSegment(24, 25, 5, j, 3);
				if (((seg_data[26] >> j) & 0x1) != 0)
					drawSegment(48, 25, 5, j, 3);
				if (((seg_data[27] >> j) & 0x1) != 0)
					drawSegment(56, 25, 5, j, 3);
			}
		}

		void _2x7Num_4x1Num_1x16Alpha(byte[] seg_data)
		{
			byte i, j;
			for (i = 0; i < 14; i++) {
				for (j = 0; j < 16; j++) {
					// 2x7 numeric
					if (((seg_data[i] >> j) & 0x1) != 0)
						drawSegment((i + ((i < 7) ? 0 : 2)) * 8, 0, 1, j, 3);
					smoothDigitCorners((i + ((i < 7) ? 0 : 2)) * 8, 0);
				}
			}
			// 4x1 numeric small
			for (j = 0; j < 16; j++) {
				if (((seg_data[14] >> j) & 0x1) != 0)
					drawSegment(16, 12, 5, j, 3);
				if (((seg_data[15] >> j) & 0x1) != 0)
					drawSegment(24, 12, 5, j, 3);
				if (((seg_data[16] >> j) & 0x1) != 0)
					drawSegment(40, 12, 5, j, 3);
				if (((seg_data[17] >> j) & 0x1) != 0)
					drawSegment(48, 12, 5, j, 3);
			}
			// 1x16 alphanumeric
			for (i = 0; i < 12; i++) {
				for (j = 0; j < 16; j++) {
					if (((seg_data[i + 18] >> j) & 0x1) != 0)
						drawSegment((i * 8) + 16, 21, 0, j, 3);
					smoothDigitCorners((i * 8) + 16, 21);
				}
			}
		}

		void _1x16Alpha_1x16Num_1x7Num(byte[] seg_data)
		{
			byte i, j;

			// 1x16 alphanumeric
			for (i = 0; i < 16; i++) {
				for (j = 0; j < 16; j++) {
					if (((seg_data[i] >> j) & 0x1) != 0)
						drawSegment((i * 8), 1, 0, j, 3);
					smoothDigitCorners((i * 8), 1);
				}
			}

			// 1x16 numeric
			for (i = 0; i < 16; i++) {
				for (j = 0; j < 16; j++) {
					if (((seg_data[i + 16] >> j) & 0x1) != 0)
						drawSegment((i * 8), 21, 1, j, 3);
					smoothDigitCorners((i * 8), 21);
				}
			}

			// 1x7 numeric small
			for (i = 0; i < 7; i++) {
				for (j = 0; j < 16; j++) {
					if (((seg_data[i + 32] >> j) & 0x1) != 0)
						drawSegment(i * 8, 13, 5, j, 3);
				}
			}
		}
	}
}
