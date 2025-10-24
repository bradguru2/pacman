// GameAudio.cs
//
// Simple audio wrapper using Silk.NET.OpenAL to load a PCM WAV file and play it.
// This class is intentionally minimal and robust: it loads the WAV into an OpenAL
// buffer and plays it on a source when PlayChomp() is called.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Silk.NET.OpenAL;

namespace PacMan
{
    unsafe public sealed class GameAudio : IDisposable
    {
        private readonly string _wavPath;
        private readonly ALContext _alc;
        private readonly AL _al;

        private Device* _device;   // ALCdevice*
        private Context* _context;  // ALCcontext*

        private uint _buffer = 0;
        private uint _source = 0;

        private bool _initialized = false;

        /// <summary>
        /// Create a GameAudio that will load WAV from the given relative path.
        /// Example path: "Assets/Audio/waka.wav"
        /// </summary>
        public GameAudio(string wavPath)
        {
            _wavPath = wavPath ?? throw new ArgumentNullException(nameof(wavPath));
            _alc = ALContext.GetApi();
            _al = AL.GetApi();
        }

        /// <summary>
        /// Initialize OpenAL, create a device/context, load WAV and create source.
        /// Call from your main thread before use (e.g., in Program.OnLoad).
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            // Open default device
            _device = _alc.OpenDevice(null);
            if (_device == null)
                throw new InvalidOperationException("Failed to open default OpenAL device.");

            // Create context
            _context = _alc.CreateContext(_device, (int*) null);
            if (_context == null)
            {
                _alc.CloseDevice(_device);
                throw new InvalidOperationException("Failed to create OpenAL context.");
            }

            // Make context current
            if (!_alc.MakeContextCurrent(_context))
            {
                _alc.DestroyContext(_context);
                _alc.CloseDevice(_device);
                throw new InvalidOperationException("Failed to make OpenAL context current.");
            }

            // Generate a buffer and source
            _buffer = _al.GenBuffer();
            _source = _al.GenSource();

            // Load WAV data into buffer
            LoadWavToBuffer(_wavPath, _buffer);

            // Attach buffer to source
            uint[] buffers = [_buffer];
            fixed (uint* b = buffers)
            _al.SourceQueueBuffers(_source, 1, b);

            _initialized = true;
        }

        /// <summary>
        /// Play the chomp sound once. Overlapping playback is handled by restarting the source.
        /// </summary>
        public void PlayChomp()
        {
            if (!_initialized) return;

            // Stop, rewind, and play so we get a fresh one-shot each time.
            _al.SourceStop(_source);
            _al.SourceRewind(_source);

            // If using queue approach, unqueue then requeue; but simpler: set buffer directly
            _al.SetSourceProperty(_source, SourceInteger.Buffer, (int)_buffer);
            _al.SourcePlay(_source);
        }

        /// <summary>
        /// Load a 16-bit PCM WAV file into the given OpenAL buffer.
        /// Supports PCM 8/16-bit, mono/stereo. Only basic WAV header parsing.
        /// Throws if WAV isn't PCM.
        /// </summary>
        private unsafe void LoadWavToBuffer(string path, uint buffer)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"WAV file not found: {path}");

            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            // --- Read WAV header (RIFF) ---
            // RIFF chunk
            var riff = new string(br.ReadChars(4));
            if (riff != "RIFF")
                throw new InvalidDataException("WAV file missing RIFF header.");

            _ = br.ReadInt32(); // skip chunk size
            var wave = new string(br.ReadChars(4));
            if (wave != "WAVE")
                throw new InvalidDataException("WAV file missing WAVE header.");

            // Read chunks until "fmt " and "data" found
            int audioFormat = 0;
            int numChannels = 0;
            int sampleRate = 0;
            int bitsPerSample = 0;
            byte[] dataBytes = null;

            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                var chunkId = new string(br.ReadChars(4));
                var chunkSize = br.ReadInt32();

                if (chunkId == "fmt ")
                {
                    audioFormat = br.ReadInt16();           // PCM = 1
                    numChannels = br.ReadInt16();
                    sampleRate = br.ReadInt32();
                    _ = br.ReadInt32();                     // byte rate
                    _ = br.ReadInt16();                     // block align
                    bitsPerSample = br.ReadInt16();

                    // If fmt chunk has extra bytes, skip them
                    var fmtExtra = chunkSize - 16;
                    if (fmtExtra > 0)
                        br.ReadBytes(fmtExtra);
                }
                else if (chunkId == "data")
                {
                    dataBytes = br.ReadBytes(chunkSize);
                }
                else
                {
                    // skip unknown chunk
                    br.ReadBytes(chunkSize);
                }

                // align to word boundary if needed (not strictly necessary)
            }

            if (audioFormat != 1)
                throw new InvalidDataException("Only PCM WAV files are supported.");

            if (dataBytes == null)
                throw new InvalidDataException("WAV file contains no data chunk.");

            // Determine AL format
            BufferFormat alFormat;
            if (bitsPerSample == 8)
            {
                alFormat = (numChannels == 1) ? BufferFormat.Mono8 : BufferFormat.Stereo8;
            }
            else if (bitsPerSample == 16)
            {
                alFormat = (numChannels == 1) ? BufferFormat.Mono16 : BufferFormat.Stereo16;
            }
            else
            {
                throw new InvalidDataException($"Unsupported bits-per-sample: {bitsPerSample}");
            }

            // Upload to OpenAL buffer
            fixed (byte* dataPtr = dataBytes)
            {
                // BufferData signature: void BufferData(uint buffer, ALFormat format, void* data, int size, int freq)
                _al.BufferData(buffer, alFormat, dataPtr, dataBytes.Length, sampleRate);
            }
        }

        /// <summary>
        /// Clean up OpenAL resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_al.Context != null && _source != 0)
                {
                    _al.SourceStop(_source);
                    _al.DeleteSource(_source);
                    _source = 0;
                }

                if (_buffer != 0)
                {
                    _al.DeleteBuffer(_buffer);
                    _buffer = 0;
                }

                if (_context != null)
                {
                    _alc.DestroyContext(_context);
                    _context = null;
                }

                if (_device != null)
                {
                    _alc.CloseDevice(_device);
                    _device = null;
                }
            }
            catch
            {
                // swallow exceptions on dispose
            }
        }
    }
}
