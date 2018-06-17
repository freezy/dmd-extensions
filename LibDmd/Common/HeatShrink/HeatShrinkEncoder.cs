using System;
using System.IO;

namespace LibDmd.Common.HeatShrink
{
	/// <summary>
	/// Implementation of the HeatShrink compression algorithm
	/// </summary>
	/// 
	/// <see cref="https://github.com/atomicobject/heatshrink/blob/master/heatshrink_encoder.c">Original Implementation</see>
	/// <see cref="https://github.com/sker65/heatshrink-java/blob/master/src/main/java/com/rinke/solutions/io/HeatShrinkEncoder.java">Java Implementation by Stefan Rinke</see>
	public class HeatShrinkEncoder
	{
		private const int MatchNotFound = -1;

		private int[] _index;

		private const int FlagIsFinishing = 1;
		private const byte HeatshrinkLiteralMarker = 0x01;
		private const byte HeatshrinkBackrefMarker = 0x00;

		private int _inputSize; /* bytes in input buffer */
		private Match _match = new Match(0, 0);
		private int _outgoingBits; /* enqueued outgoing bits */
		private int _outgoingBitsCount;
		private int _flags;
		private State _state; /* current state machine node */
		private int _currentByte; /* current byte of output */
		private int _bitIndex; /* current bit index */

		private readonly int _windowSize;
		private readonly int _lookAhead;

		/* input buffer and / sliding window for expansion */
		private readonly byte[] _buffer;// = new byte[2 << HEATSHRINK_STATIC_WINDOW_BITS];
		private readonly bool _useIndex = true;

		public HeatShrinkEncoder(int windowSize, int lookAhead)
		{
			_windowSize = windowSize;
			_lookAhead = lookAhead;
			_buffer = new byte[2 << windowSize];
			_index = new int[2 << windowSize];
			Reset();
		}

		public void Reset()
		{
			for (var i = 0; i < _buffer.Length; i++) {
				_buffer[i] = 0;
			}
			_inputSize = 0;
			_state = State.HsesNotFull;
			_flags = 0;
			_bitIndex = 0x80;
			_currentByte = 0x00;
			_match.ScanIndex = 0;
			_match.Length = 0;
			_outgoingBits = 0x0000;
			_outgoingBitsCount = 0;
			_index = new int[2 << _windowSize];
		}

		/*
		 * Sink up to SIZE bytes from IN_BUF into the encoder. INPUT_SIZE is set to
		 * the number of bytes actually sunk (in case a buffer was filled.).
		 */
		public Result Sink(byte[] inputBuffer, int offset, int size /* , size_t *input_size */)
		{
			if (inputBuffer == null) {
				throw new ArgumentException("inputBuffer must not be null");
			}
			/* Sinking more content after saying the content is done, tsk tsk */
			if (IsFinishing()) {
				throw new ArgumentException("encoder is already in finished state");
			}

			/* Sinking more content before processing is done */
			if (_state != State.HsesNotFull) {
				throw new ArgumentException("Sinking more content before processing is done");
			}

			var writeOffset = GetInputOffset() + _inputSize;
			var inputBufferSize = GetInputBufferSize();
			var remain = inputBufferSize - _inputSize;
			var copySize = remain < size ? remain : size;

			// memcpy(&hse->buffer[write_offset], in_buf, cp_sz);
			Buffer.BlockCopy(inputBuffer, offset, _buffer, writeOffset, copySize);
			// *input_size = cp_sz;

			_inputSize += copySize;

			if (copySize == remain) {
				_state = State.HsesFilled;
				return Result.res(copySize, Code.Full);
			}

			return Result.res(copySize, Code.Ok);
		}

		private int GetInputBufferSize()
		{
			return 1 << _windowSize;
		}

		private int GetInputOffset()
		{
			return GetInputBufferSize(); //
		}

		private bool IsFinishing()
		{
			return (_flags & FlagIsFinishing) != 0;
		}

		/// <summary>
		/// Poll for output from the encoder, copying at most OUT_BUF_SIZE bytes into
		/// OUT_BUF (setting *OUTPUT_SIZE to the actual amount copied).
		/// </summary>
		/// <param name="outBuf"></param>
		/// <returns></returns>
		public Result Poll(byte[] outBuf /* size_t *output_size */)
		{
			if (outBuf == null) {
				throw new ArgumentException("outBuf must not be null");
			}

			var outBufSize = outBuf.Length;

			if (outBufSize == 0) {
				throw new ArgumentException("outBuf length must not be null");
			}

			var oi = new OutputInfo {
				Buf = outBuf,
				BufSize = outBuf.Length,
				OutputSize = 0
			};

			while (true) {

				var inState = _state;
				switch (inState) {
					case State.HsesNotFull:
						return Result.res(oi.OutputSize, Code.Empty);
					case State.HsesFilled:
						DoIndexing();
						_state = State.HsesSearch;
						break;
					case State.HsesSearch:
						_state = StepSearch();
						break;
					case State.HsesYieldTagBit:
						_state = YieldTagBit(oi);
						break;
					case State.HsesYieldLiteral:
						_state = YieldLiteral(oi);
						break;
					case State.HsesYieldBrIndex:
						_state = YieldBackRefIndex(oi);
						break;
					case State.HsesYieldBrLength:
						_state = YieldBackRefLength(oi);
						break;
					case State.HsesSaveBacklog:
						_state = SaveBacklog();
						break;
					case State.HsesFlushBits:
						_state = FlushBitBuffer(oi);
						return Result.res(oi.OutputSize, Code.Empty);
					case State.HsesDone:
						return Result.res(oi.OutputSize, Code.Empty);
					default:
						return Result.res(oi.OutputSize, Code.ErrorMisuse);
				}

				if (_state == inState) {
					/* Check if output buffer is exhausted. */
					if (oi.OutputSize == outBufSize)
						return Result.res(oi.OutputSize, Code.More);
				}
			}
			// return new PollRes(output_size, PollRes.Res.EMPTY);
		}

		private State FlushBitBuffer(OutputInfo oi)
		{
			if (_bitIndex == 0x80) {
				return State.HsesDone;
			} else if (CanTakeByte(oi)) {
				oi.Buf[oi.OutputSize++] = (byte)_currentByte;
				return State.HsesDone;
			} else {
				return State.HsesFlushBits;
			}
		}

		private State SaveBacklog()
		{
			var inputBufferSize = GetInputBufferSize();

			/*
			 * Copy processed data to beginning of buffer, so it can be used for
			 * future matches. Don't bother checking whether the input is less than
			 * the maximum size, because if it isn't, we're done anyway.
			 */
			var rem = inputBufferSize - _match.ScanIndex; // unprocessed bytes
			var shiftSize = inputBufferSize + rem;

			// memmove(&hse->buffer[0],
			// &hse->buffer[input_buf_sz - rem],
			// shift_sz);
			// to: buffer, from: buffer + (input_buf_sz - rem) 
			// amount: shift_sz
			var offset = inputBufferSize - rem;
			Buffer.BlockCopy(_buffer, offset, _buffer, 0, shiftSize);
			_match.ScanIndex = 0;
			_inputSize -= offset;

			return State.HsesNotFull;
		}

		private State YieldBackRefLength(OutputInfo oi)
		{
			if (CanTakeByte(oi)) {
				if (push_outgoing_bits(oi) > 0) {
					return State.HsesYieldBrLength;
				} else {
					_match.ScanIndex += _match.Length;
					_match.Length = 0;
					return State.HsesSearch;
				}
			} else {
				return State.HsesYieldBrLength;
			}
		}

		private int push_outgoing_bits(OutputInfo oi)
		{
			int count;
			byte bits;
			if (_outgoingBitsCount > 8) {
				count = 8;
				bits = (byte)(_outgoingBits >> (_outgoingBitsCount - 8));
			} else {
				count = _outgoingBitsCount;
				bits = (byte)_outgoingBits;
			}

			if (count > 0) {
				PushBits(count, bits, oi);
				_outgoingBitsCount -= count;
			}
			return count;
		}

		private State YieldBackRefIndex(OutputInfo oi)
		{
			if (CanTakeByte(oi)) {
				if (push_outgoing_bits(oi) > 0) {
					return State.HsesYieldBrIndex; /* continue */
				} else {
					_outgoingBits = _match.Length - 1;
					_outgoingBitsCount = _lookAhead;
					return State.HsesYieldBrLength; /* done */
				}
			} else {
				return State.HsesYieldBrIndex; /* continue */
			}
		}

		private State YieldLiteral(OutputInfo oi)
		{
			if (CanTakeByte(oi)) {
				push_literal_byte(oi);
				return State.HsesSearch;
			} else {
				return State.HsesYieldLiteral;
			}
		}

		private void push_literal_byte(OutputInfo oi)
		{
			var processedOffset = _match.ScanIndex - 1;
			var inputOffset = GetInputOffset() + processedOffset;
			var c = _buffer[inputOffset];
			PushBits(8, c, oi);

		}

		private State YieldTagBit(OutputInfo oi)
		{
			if (CanTakeByte(oi)) {
				if (_match.Length == 0) {
					AddTagBit(oi, HeatshrinkLiteralMarker);
					return State.HsesYieldLiteral;
				}
				AddTagBit(oi, HeatshrinkBackrefMarker);
				_outgoingBits = _match.Pos - 1;
				_outgoingBitsCount = _windowSize;
				return State.HsesYieldBrIndex;
			}
			return State.HsesYieldTagBit; /* output is full, continue */
		}

		private bool CanTakeByte(OutputInfo oi)
		{
			return oi.OutputSize < oi.BufSize;
		}

		private void AddTagBit(OutputInfo oi, byte tag)
		{
			PushBits(1, tag, oi);
		}

		private void PushBits(int count, byte bits, OutputInfo oi)
		{

			/*
			 * If adding a whole byte and at the start of a new output byte, just
			 * push it through whole and skip the bit IO loop.
			 */
			if (count == 8 && _bitIndex == 0x80) {
				oi.Buf[oi.OutputSize++] = bits;
			} else {
				for (var i = count - 1; i >= 0; i--) {
					var bit = (bits & (1 << i)) != 0;
					if (bit) {
						_currentByte |= _bitIndex;
					}
					// if (false) {
					// log.debug("  -- setting bit %d at bit index 0x%02x, byte => 0x%02x",
					// bit ? 1 : 0, bit_index, current_byte));
					// }
					_bitIndex >>= 1;
					if (_bitIndex == 0x00) {
						_bitIndex = 0x80;
						oi.Buf[oi.OutputSize++] = (byte)_currentByte;
						_currentByte = 0x00;
					}
				}
			}

		}

		private State StepSearch()
		{
			var windowLength = GetInputBufferSize();
			var lookaheadSz = GetLookaheadSize();
			var msi = _match.ScanIndex;

			var fin = IsFinishing();
			if (msi > _inputSize - (fin ? 1 : lookaheadSz)) {
				/*
				 * Current search buffer is exhausted, copy it into the backlog and
				 * await more input.
				 */
				return fin ? State.HsesFlushBits : State.HsesSaveBacklog;
			}

			var inputOffset = GetInputOffset();
			var end = inputOffset + msi;
			var start = end - windowLength;

			var maxPossible = lookaheadSz;
			if (_inputSize - msi < lookaheadSz) {
				maxPossible = _inputSize - msi;
			}

			_match = FindLongestMatch(start, end, maxPossible /* , &match_length */);

			if (_match.Pos == MatchNotFound) {
				_match.ScanIndex++;
				_match.Length = 0;
				return State.HsesYieldTagBit;
			} else {
				// match_pos = match_pos;
				// match_length = match_length;
				// ASSERT(match_pos <= 1 << HEATSHRINK_ENCODER_WINDOW_BITS(hse)
				// /*window_length*/);

				return State.HsesYieldTagBit;
			}
		}

		private class Match
		{
			public int Pos;
			public int Length;
			public int ScanIndex;

			public Match(int pos, int length)
			{
				Pos = pos;
				Length = length;
			}
		}

		private Match FindLongestMatch(int start, int end, int maxlen)
		{

			var matchMaxlen = 0;
			var matchIndex = MatchNotFound;

			int len;
			var needlepointIdx = end; // "points into buffer"

			if (_useIndex) {
				// struct hs_index *hsi = HEATSHRINK_ENCODER_INDEX(hse);
				var pos = _index[end];

				while (pos - start >= 0) {
					var pospointIdx = pos; // "points into buffer"

					/*
					 * Only check matches that will potentially beat the current maxlen.
					 * This is redundant with the index if match_maxlen is 0, but the
					 * added branch overhead to check if it == 0 seems to be worse.
					 */
					if (_buffer[pospointIdx + matchMaxlen] != _buffer[needlepointIdx + matchMaxlen]) {
						pos = _index[pos];
						continue;
					}

					for (len = 1; len < maxlen; len++) {
						if (_buffer[pospointIdx + len] != _buffer[needlepointIdx + len])
							break;
					}

					if (len > matchMaxlen) {
						matchMaxlen = len;
						matchIndex = pos;
						if (len == maxlen) {
							break;
						} /* won't find better */
					}
					pos = _index[pos];
				}

			} else {
				for (var pos = end - 1; pos - start >= 0; pos--) {
					var pospointIdx = pos;

					if ((_buffer[pospointIdx + matchMaxlen] == _buffer[needlepointIdx + matchMaxlen])
						&& (_buffer[pospointIdx] == _buffer[needlepointIdx])) {
						for (len = 1; len < maxlen; len++) {
							//		                if (0) {
							//		                    LOG("  --> cmp buf[%d] == 0x%02x against %02x (start %u)\n",
							//		                        pos + len, pospoint[len], needlepoint[len], start);
							//		                }
							if (_buffer[pospointIdx + len] != _buffer[needlepointIdx + len]) { break; }
						}
						if (len > matchMaxlen) {
							matchMaxlen = len;
							matchIndex = pos;
							if (len == maxlen) { break; } /* don't keep searching */
						}
					}
				}

			} // use index


			var breakEvenPoint = (1 + _windowSize + _lookAhead);

			/*
			 * Instead of comparing break_even_point against 8*match_maxlen, compare
			 * match_maxlen against break_even_point/8 to avoid overflow. Since
			 * MIN_WINDOW_BITS and MIN_LOOKAHEAD_BITS are 4 and 3, respectively,
			 * break_even_point/8 will always be at least 1.
			 */
			if (matchMaxlen > (breakEvenPoint / 8)) {
				_match.Length = matchMaxlen;
				_match.Pos = end - matchIndex;
				return _match;
			}
			_match.Pos = MatchNotFound;
			return _match;
		}

		private int GetLookaheadSize()
		{
			return 1 << _lookAhead;
		}

		private void DoIndexing()
		{
			/*
			 * Build an index array I that contains flattened linked lists for the
			 * previous instances of every byte in the buffer.
			 * 
			 * For example, if buf[200] == 'x', then index[200] will either be an
			 * offset i such that buf[i] == 'x', or a negative offset to indicate
			 * end-of-list. This significantly speeds up matching, while only using
			 * sizeof(uint16_t)*sizeof(buffer) bytes of RAM.
			 * 
			 * Future optimization options: 1. Since any negative value represents
			 * end-of-list, the other 15 bits could be used to improve the index
			 * dynamically.
			 * 
			 * 2. Likewise, the last lookahead_sz bytes of the index will not be
			 * usable, so temporary data could be stored there to dynamically
			 * improve the index.
			 */
			// struct hs_index *hsi = HEATSHRINK_ENCODER_INDEX(hse);
			var last = new int[256];

			for (var i = 0; i < last.Length; i++) { // memset(last, 0xFF, sizeof(last));
				last[i] = -1;
			}

			var inputOffset = GetInputOffset();
			var end = inputOffset + _inputSize;

			for (var i = 0; i < end; i++) {
				var v = _buffer[i] & 0xFF;
				var lv = last[v];
				_index[i] = lv;
				last[v] = i;
			}
		}

		/*
		 * Notify the encoder that the input stream is finished. If the return value
		 * is HSER_FINISH_MORE, there is still more output, so call
		 * heatshrink_encoder_poll and repeat.
		 */
		public Result Finish()
		{
			_flags |= FlagIsFinishing;
			if (_state == State.HsesNotFull) {
				_state = State.HsesFilled;
			}
			return _state == State.HsesDone ? Result.res(Code.Done) : Result.res(Code.More);
		}

		public void Encode(Stream reader, Stream writer)
		{
			var inbuffer = new byte[1024];
			var outbuffer = new byte[4096];
			//System.setProperty("org.slf4j.simpleLogger.defaultLogLevel", "warn");
			var inputOffset = 0;
			var remainingInInput = 0;
			while (true) {
				Result res;
				do { // read and fill input buffer until full
					if (remainingInInput == 0) {
						// read some input bytes
						remainingInInput = reader.Read(inbuffer, 0, 4096);
						inputOffset = 0;
					}
					if (remainingInInput == 0) {
						res = Finish();
						break;
					}
					res = Sink(inbuffer, inputOffset, remainingInInput);
					if (res.IsError()) throw new Exception("error poll");
					remainingInInput -= res.Count;
					inputOffset += res.Count;
				} while (res.Code != Code.Full);

				if (res.Code == Code.Done) break;
				// now input buffer is full, poll for output
				do {
					res = Poll(outbuffer);
					if (res.IsError()) throw new Exception("error poll");
					if (res.Count > 0) {
						writer.Write(outbuffer, 0, res.Count);
					}
				} while (res.Code == Code.More);
				//if( res.code == DONE ) break;
			}
		}
		enum State
		{
			/// <summary>
			/// input buffer not full enough
			/// </summary>
			HsesNotFull,
			/// <summary>
			/// buffer is full
			/// </summary>
			HsesFilled,
			/// <summary>
			/// searching for patterns
			/// </summary>
			HsesSearch,
			/// <summary>
			/// yield tag bit
			/// </summary>
			HsesYieldTagBit,
			/// <summary>
			/// emit literal byte
			/// </summary>
			HsesYieldLiteral,
			/// <summary>
			/// yielding backref index
			/// </summary>
			HsesYieldBrIndex,
			/// <summary>
			/// yielding backref length
			/// </summary>
			HsesYieldBrLength,
			/// <summary>
			/// copying buffer to backlog
			/// </summary>
			HsesSaveBacklog,
			/// <summary>
			/// flush bit buffer
			/// </summary>
			HsesFlushBits,
			/// <summary>
			/// done
			/// </summary>
			HsesDone
		}
	}
}
