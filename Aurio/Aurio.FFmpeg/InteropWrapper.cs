﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aurio.FFmpeg {
    /// <summary>
    /// Wraps the x86 and x64 interop functions and provides the correct ones depending on the execution platform.
    /// </summary>
    internal class InteropWrapper {

        // It would be cleaner/shorter to use Func<> pointers to save the delegate definitions, 
        // but they are not defined for out parameters
        // http://stackoverflow.com/a/20560385

        public delegate IntPtr d_stream_open(string filename);
        public delegate IntPtr d_stream_get_output_config(IntPtr instance);
        public delegate int d_stream_read_frame(IntPtr instance, out long timestamp, byte[] output_buffer, int output_buffer_size);
        public delegate void d_stream_seek(IntPtr instance, long timestamp);
        public delegate void d_stream_close(IntPtr instance);

        public static d_stream_open stream_open;
        public static d_stream_get_output_config stream_get_output_config;
        public static d_stream_read_frame stream_read_frame;
        public static d_stream_seek stream_seek;
        public static d_stream_close stream_close;

        static InteropWrapper() {
            if (Environment.Is64BitProcess) {
                stream_open = Interop64.stream_open;
                stream_get_output_config = Interop64.stream_get_output_config;
                stream_read_frame = Interop64.stream_read_frame;
                stream_seek = Interop64.stream_seek;
                stream_close = Interop64.stream_close;
            }
            else {
                stream_open = Interop32.stream_open;
                stream_get_output_config = Interop32.stream_get_output_config;
                stream_read_frame = Interop32.stream_read_frame;
                stream_seek = Interop32.stream_seek;
                stream_close = Interop32.stream_close;
            }
        }
    }
}