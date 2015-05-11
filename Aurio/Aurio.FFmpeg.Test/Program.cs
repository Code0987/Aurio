﻿using Aurio.Streams;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Aurio.FFmpeg.Test {
    class Program {
        static void Main(string[] args) {
            if (args.Length == 0) {
                Console.WriteLine("no input file specified");
                return;
            }

            // TODO read audio from FFmpeg
            FFmpegReader reader = new FFmpegReader(args[0]);

            Console.WriteLine("length {0}, frame_size {1}, sample_rate {2}, sample_size {3}, channels {4}", 
                reader.OutputConfig.length,
                reader.OutputConfig.frame_size,
                reader.OutputConfig.format.sample_rate,
                reader.OutputConfig.format.sample_size,
                reader.OutputConfig.format.channels);

            int sampleBlockSize = reader.OutputConfig.format.channels * reader.OutputConfig.format.sample_size;

            int output_buffer_size = reader.OutputConfig.frame_size * 
                reader.OutputConfig.format.channels * reader.OutputConfig.format.sample_size;
            byte[] output_buffer = new byte[output_buffer_size];

            int samplesRead;
            long timestamp;
            MemoryStream ms = new MemoryStream();

            // read full stream
            while ((samplesRead = reader.ReadFrame(out timestamp, output_buffer, output_buffer_size)) > 0) {
                Console.WriteLine("read " + samplesRead + " @ " + timestamp);

                // read samples into memory
                int bytesRead = samplesRead * sampleBlockSize;
                ms.Write(output_buffer, 0, bytesRead);
            }

            // seek back to start
            reader.Seek(0);

            // read again (output should be the same as above)
            while ((samplesRead = reader.ReadFrame(out timestamp, output_buffer, output_buffer_size)) > 0) {
                Console.WriteLine("read " + samplesRead + " @ " + timestamp);
            }

            reader.Dispose();

            // write memory to wav file
            ms.Position = 0;
            MemorySourceStream mss = new MemorySourceStream(ms, new AudioProperties(
                reader.OutputConfig.format.channels, 
                reader.OutputConfig.format.sample_rate, 
                reader.OutputConfig.format.sample_size * 8, 
                reader.OutputConfig.format.sample_size == 4 ? AudioFormat.IEEE : AudioFormat.LPCM));
            IeeeStream ieee = new IeeeStream(mss);
            NAudioSinkStream nAudioSink = new NAudioSinkStream(ieee);
            WaveFileWriter.CreateWaveFile(args[0] + ".ffmpeg.wav", nAudioSink);

        }
    }
}
