using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Scorbit
{
    public delegate void LoggerDelegate(string message);

    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Err = 4,
        Critical = 5,
        Off = 6
    }

    public struct FrameInfoCli
    {
        public byte[] FrameBuffer;
        public int DecoderVersion;
        public int DisplayFamily;
        public int DisplayId;
        public int Width;
        public int Height;
        public int Colors;
        public bool Error;
        public bool Duplicate;
    }

    public interface IScorbitProcessor
    {
        void Init(LoggerDelegate logger, LogLevel logLevel);
        void Process(ref FrameInfoCli frameCli);

        // Properties
        string Version { get; }
        string ProjectName { get; }
    }
}
