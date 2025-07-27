using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Jacobi.Vst.Core.Host;
using Jacobi.Vst.Host.Interop;
using NAudio.Wave;

namespace Teston.Vst
{
    public class VstChain : IDisposable
    {
        private readonly int _blockSize;
        private readonly float _sampleRate;
        private readonly IVstPluginLoader _loader;
        private readonly List<(VstManager manager, VstPluginContext context)> _plugins = new List<(VstManager, VstPluginContext)>();
        private bool _disposed;

        public IEnumerable<VstManager> Managers => _plugins.Select(p => p.manager);

        public VstChain(int blockSize, float sampleRate)
        {
            _blockSize = blockSize;
            _sampleRate = sampleRate;
            _loader = new VstPluginLoader();
        }

        public void AddPlugin(string vstPath)
        {
            if (!File.Exists(vstPath))
            {
                throw new FileNotFoundException($"VST плагин не найден: {vstPath}");
            }

            var hostCmdStub = new DummyHostCommandStub
            {
                BlockSize = _blockSize,
                SampleRate = _sampleRate
            };
            hostCmdStub.PluginCalled += (sender, e) => Console.WriteLine($"Плагин вызвал: {e.Message}");

            VstPluginContext context = _loader.LoadPlugin(vstPath, hostCmdStub);
            VstManager manager = new VstManager(context, _blockSize, _sampleRate);

            _plugins.Add((manager, context));
            Console.WriteLine($"Плагин '{manager.PluginName}' добавлен в цепочку. Входы: {manager.InputCount}, Выходы: {manager.OutputCount}");
        }

        private ISampleProvider CreateChainedProvider(ISampleProvider source)
        {
            ISampleProvider current = source;
            foreach (var plugin in _plugins)
            {
                Console.WriteLine($"Добавление VST в цепочку: {plugin.manager.PluginName}");
                current = new VstAudioProvider(current, plugin.manager, _blockSize);
            }
            return current;
        }

        public void ProcessAndPlay(string audioPath)
        {
            if (!File.Exists(audioPath))
            {
                throw new FileNotFoundException($"Аудиофайл не найден: {audioPath}");
            }

            Console.WriteLine($"Запуск ProcessAndPlay для {audioPath}. Плагинов в цепочке: {_plugins.Count}");

            if (_plugins.Count == 0)
            {
                Console.WriteLine("Предупреждение: Цепочка плагинов пуста. Воспроизведение без обработки.");
            }

            using var audioReader = new WaveFileReader(audioPath);
            if (audioReader.WaveFormat.SampleRate != _sampleRate)
            {
                Console.WriteLine("Предупреждение: SampleRate аудиофайла отличается от настроенного в цепочке.");
            }

            var sourceProvider = audioReader.ToSampleProvider();
            var processedProvider = CreateChainedProvider(sourceProvider);

            var outputFormat = processedProvider.WaveFormat;
            var outputChannels = outputFormat.Channels;

            var waveProvider = new BufferedWaveProvider(outputFormat);
            using var waveOut = new WaveOutEvent();
            waveOut.Init(waveProvider);

            Console.WriteLine("\nНачинаем обработку и воспроизведение через цепочку...");
            waveOut.Play();

            var playbackBufferFloat = new float[_blockSize * outputChannels];

            int blockCount = 0;
            while (true)
            {
                int samplesRead = processedProvider.Read(playbackBufferFloat, 0, playbackBufferFloat.Length);
                if (samplesRead == 0) break;

                var playbackBufferBytes = new byte[samplesRead * sizeof(float)];
                Buffer.BlockCopy(playbackBufferFloat, 0, playbackBufferBytes, 0, playbackBufferBytes.Length);
                waveProvider.AddSamples(playbackBufferBytes, 0, playbackBufferBytes.Length);

                blockCount++;
                if (blockCount % 10 == 0) Console.WriteLine($"Обработано блоков: {blockCount}");

                while (waveProvider.BufferedDuration.TotalSeconds > 0.3)
                {
                    Thread.Sleep(50);
                }
            }

            Console.WriteLine("Обработка завершена. Ожидание окончания воспроизведения...");
            while (waveOut.PlaybackState == PlaybackState.Playing && waveProvider.BufferedBytes > 0)
            {
                Thread.Sleep(100);
            }
            Console.WriteLine("Воспроизведение завершено.");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var plugin in _plugins)
                {
                    plugin.manager.Dispose();
                    _loader.UnloadPlugin(plugin.context);
                }
                _plugins.Clear();
                _disposed = true;
            }
        }
    }
}