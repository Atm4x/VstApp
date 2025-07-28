using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jacobi.Vst.Core.Host;
using Jacobi.Vst.Host.Interop;
using NAudio.Wave;

namespace Teston.Vst
{
    public class VstChain : IDisposable
    {
        private readonly List<(VstManager manager, VstPluginContext context)> _plugins = new List<(VstManager, VstPluginContext)>();
        private readonly IVstPluginLoader _loader;
        private bool _disposed;

        public int BlockSize { get; }
        public float SampleRate { get; }
        public IEnumerable<VstManager> Managers => _plugins.Select(p => p.manager);

        public VstChain(int blockSize, float sampleRate)
        {
            BlockSize = blockSize;
            SampleRate = sampleRate;
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
                BlockSize = BlockSize,
                SampleRate = SampleRate
            };
            hostCmdStub.PluginCalled += (sender, e) => Console.WriteLine($"Плагин вызвал: {e.Message}");

            VstPluginContext context = _loader.LoadPlugin(vstPath, hostCmdStub);
            VstManager manager = new VstManager(context, BlockSize, SampleRate);

            _plugins.Add((manager, context));
            Console.WriteLine($"Плагин '{manager.PluginName}' добавлен в цепочку. Входы: {manager.InputCount}, Выходы: {manager.OutputCount}");
        }

        public void MovePlugin(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= _plugins.Count || newIndex < 0 || newIndex >= _plugins.Count)
                return;

            var plugin = _plugins[oldIndex];
            _plugins.RemoveAt(oldIndex);
            _plugins.Insert(newIndex, plugin);
            Console.WriteLine($"Плагин перемещён с позиции {oldIndex} на {newIndex}");
        }

        public ISampleProvider CreateChainedProvider(ISampleProvider source)
        {
            ISampleProvider current = source;
            foreach (var plugin in _plugins)
            {
                Console.WriteLine($"Добавление VST в цепочку: {plugin.manager.PluginName}");
                current = new VstAudioProvider(current, plugin.manager, BlockSize);
            }
            return current;
        }

        public static WaveStream CreateAudioReader(string audioPath)
        {
            var extension = Path.GetExtension(audioPath).ToLowerInvariant().TrimStart('.');

            return extension switch
            {
                "wav" => new WaveFileReader(audioPath),
                "mp3" or "flac" or "ogg" => new MediaFoundationReader(audioPath),
                _ => throw new NotSupportedException($"Формат файла не поддерживается: {extension}. Поддерживаемые: wav, mp3, flac, ogg.")
            };
        }

        public static WaveFormat GetAudioFormat(string audioPath)
        {
            using var reader = CreateAudioReader(audioPath);
            return reader.WaveFormat;
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