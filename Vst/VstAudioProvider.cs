using System;
using NAudio.Wave;

namespace Teston.Vst
{
    public class VstAudioProvider : ISampleProvider
    {
        private readonly ISampleProvider _sourceProvider;
        private readonly VstManager _vstManager;
        private readonly int _blockSize;
        private readonly float[][] _inputBuffers;
        private readonly float[][] _outputBuffers;
        private readonly float[] _readBuffer;
        private readonly WaveFormat _outputFormat;

        public WaveFormat WaveFormat => _outputFormat;

        public VstAudioProvider(ISampleProvider sourceProvider, VstManager vstManager, int blockSize = 1024)
        {
            _sourceProvider = sourceProvider ?? throw new ArgumentNullException(nameof(sourceProvider));
            _vstManager = vstManager ?? throw new ArgumentNullException(nameof(vstManager));
            _blockSize = blockSize;

            var sourceChannels = sourceProvider.WaveFormat.Channels;
            var vstInputs = vstManager.InputCount;
            var vstOutputs = vstManager.OutputCount;

            _outputFormat = WaveFormat.CreateIeeeFloatWaveFormat(sourceProvider.WaveFormat.SampleRate, vstOutputs);

            // Инициализация буферов
            _inputBuffers = new float[vstInputs][];
            for (int i = 0; i < vstInputs; i++)
                _inputBuffers[i] = new float[_blockSize];

            _outputBuffers = new float[vstOutputs][];
            for (int i = 0; i < vstOutputs; i++)
                _outputBuffers[i] = new float[_blockSize];

            _readBuffer = new float[_blockSize * sourceChannels];

            if (!vstManager.IsProcessing)
            {
                vstManager.StartProcessing();
                Console.WriteLine($"Старт обработки для плагина {vstManager.PluginName}");
            }
            else
            {
                Console.WriteLine($"НЕ СТАРТ обработки для плагина {vstManager.PluginName}");
            }

        }

        public int Read(float[] buffer, int offset, int count)
        {
            int totalRead = 0;
            int sourceChannels = _sourceProvider.WaveFormat.Channels;
            int outputChannels = _outputFormat.Channels;

            while (totalRead < count)
            {
                int samplesToProcess = Math.Min(_blockSize * outputChannels, count - totalRead);
                int framesToProcess = samplesToProcess / outputChannels;

                // Читаем из источника
                int samplesRead = _sourceProvider.Read(_readBuffer, 0, framesToProcess * sourceChannels);
                if (samplesRead == 0)
                    break;

                int framesRead = samplesRead / sourceChannels;

                // Очищаем буферы
                foreach (var buf in _inputBuffers)
                    Array.Clear(buf, 0, _blockSize);
                foreach (var buf in _outputBuffers)
                    Array.Clear(buf, 0, _blockSize);

                // Де-интерливинг и адаптация каналов
                DeinterleaveAndAdaptChannels(_readBuffer, _inputBuffers, framesRead, sourceChannels, _vstManager.InputCount);

                Console.WriteLine($"Чтение блока: {count} сэмплов, frames: {framesToProcess}");

                // VST обработка
                _vstManager.ProcessAudio(_inputBuffers, _outputBuffers);
                Console.WriteLine("Блок обработан VST.");

                // Интерливинг в выходной буфер
                InterleaveChannels(_outputBuffers, buffer, offset + totalRead, framesRead, outputChannels);

                totalRead += framesRead * outputChannels;
            }

            return totalRead;
        }

        private void DeinterleaveAndAdaptChannels(float[] interleavedData, float[][] channelData,
            int frames, int sourceChannels, int targetChannels)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                if (sourceChannels == targetChannels)
                {
                    // Прямое копирование
                    for (int ch = 0; ch < targetChannels; ch++)
                        channelData[ch][frame] = interleavedData[frame * sourceChannels + ch];
                }
                else if (targetChannels > sourceChannels)
                {
                    // Upmix: дублирование каналов
                    for (int ch = 0; ch < targetChannels; ch++)
                        channelData[ch][frame] = interleavedData[frame * sourceChannels + (ch % sourceChannels)];
                }
                else
                {
                    // Downmix: усреднение
                    for (int ch = 0; ch < targetChannels; ch++)
                    {
                        float sum = 0f;
                        int count = 0;
                        for (int sch = ch; sch < sourceChannels; sch += targetChannels)
                        {
                            sum += interleavedData[frame * sourceChannels + sch];
                            count++;
                        }
                        channelData[ch][frame] = count > 0 ? sum / count : 0f;
                    }
                }
            }
        }

        private void InterleaveChannels(float[][] channelData, float[] interleavedData,
            int offset, int frames, int channels)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    interleavedData[offset + frame * channels + ch] = channelData[ch][frame];
                }
            }
        }
    }
}