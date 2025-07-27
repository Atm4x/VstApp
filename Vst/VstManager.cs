using Jacobi.Vst.Core;
using Jacobi.Vst.Core.Host;
using Jacobi.Vst.Core.Plugin;
using Jacobi.Vst.Host.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Teston.Vst
{
    public unsafe class VstManager : IDisposable
    {
        private readonly VstPluginContext _context;
        private readonly Jacobi.Vst.Core.Host.IVstPluginCommandStub _pluginCommandStub;
        private readonly VstPluginInfo _pluginInfo;
        private readonly int _blockSize;
        private readonly float _sampleRate;

        private IntPtr[] _inputPtrs;
        private IntPtr[] _outputPtrs;
        private VstAudioBuffer[] _inputBuffers;
        private VstAudioBuffer[] _outputBuffers;
        private bool _isProcessing;
        private bool _disposed;

        public string PluginName => _pluginCommandStub.Commands.GetEffectName();
        public int InputCount => _pluginInfo.AudioInputCount;
        public int OutputCount => _pluginInfo.AudioOutputCount;
        public int ParameterCount => _pluginInfo.ParameterCount;
        public bool IsProcessing => _isProcessing;

        public VstManager(VstPluginContext context, int blockSize, float sampleRate)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _pluginCommandStub = context.PluginCommandStub;
            _pluginInfo = context.PluginInfo;
            _blockSize = blockSize;
            _sampleRate = sampleRate;

            InitializeBuffers();
            ConfigurePlugin();
        }

        public void OpenEditor(IntPtr parentHandle)
        {
            _pluginCommandStub.Commands.EditorOpen(parentHandle);
        }

        public void CloseEditor()
        {
            _pluginCommandStub.Commands.EditorClose();
        }

        private void InitializeBuffers()
        {
            _inputPtrs = new IntPtr[InputCount];
            _inputBuffers = new VstAudioBuffer[InputCount];
            for (int i = 0; i < InputCount; i++)
            {
                _inputPtrs[i] = Marshal.AllocHGlobal(_blockSize * sizeof(float));
                _inputBuffers[i] = new VstAudioBuffer((float*)_inputPtrs[i], _blockSize, false);
            }

            _outputPtrs = new IntPtr[OutputCount];
            _outputBuffers = new VstAudioBuffer[OutputCount];
            for (int i = 0; i < OutputCount; i++)
            {
                _outputPtrs[i] = Marshal.AllocHGlobal(_blockSize * sizeof(float));
                _outputBuffers[i] = new VstAudioBuffer((float*)_outputPtrs[i], _blockSize, true);
            }
        }

        private void ConfigurePlugin()
        {
            _pluginCommandStub.Commands.SetSampleRate(_sampleRate);
            _pluginCommandStub.Commands.SetBlockSize(_blockSize);
            _pluginCommandStub.Commands.MainsChanged(true);
        }

        public void StartProcessing()
        {
            if (!_isProcessing)
            {
                _pluginCommandStub.Commands.StartProcess();
                _isProcessing = true;
            }
        }

        public void StopProcessing()
        {
            if (_isProcessing)
            {
                _pluginCommandStub.Commands.StopProcess();
                _pluginCommandStub.Commands.MainsChanged(false);
                _isProcessing = false;
            }
        }

        public Dictionary<int, VstParameterInfo> GetParameters()
        {
            var parameters = new Dictionary<int, VstParameterInfo>();
            for (int i = 0; i < ParameterCount; i++)
            {
                parameters[i] = new VstParameterInfo
                {
                    Index = i,
                    Name = _pluginCommandStub.Commands.GetParameterName(i),
                    Display = _pluginCommandStub.Commands.GetParameterDisplay(i),
                    Label = _pluginCommandStub.Commands.GetParameterLabel(i),
                    Value = _pluginCommandStub.Commands.GetParameter(i)
                };
            }
            return parameters;
        }

        public void SetParameter(int index, float value)
        {
            if (index >= 0 && index < ParameterCount && value >= 0.0f && value <= 1.0f)
            {
                _pluginCommandStub.Commands.SetParameter(index, value);
            }
            else
            {
                throw new ArgumentOutOfRangeException("Invalid parameter index or value");
            }
        }

        public void ProcessAudio(float[][] input, float[][] output)
        {
            if (!_isProcessing)
                throw new InvalidOperationException("Processing not started");

            Console.WriteLine($"ProcessAudio вызван. Входы: {input.Length}, Выходы: {output.Length}");

            if (input == null || input.Length < InputCount)
                throw new ArgumentException($"Input array must have at least {InputCount} channels, but has {input?.Length ?? 0}");

            if (output == null || output.Length < OutputCount)
                throw new ArgumentException($"Output array must have at least {OutputCount} channels, but has {output?.Length ?? 0}");

            for (int i = 0; i < InputCount; i++)
            {
                if (input[i] == null || input[i].Length < _blockSize)
                    throw new ArgumentException($"Input channel {i} must have at least {_blockSize} samples, but has {input[i]?.Length ?? 0}");
            }

            for (int i = 0; i < OutputCount; i++)
            {
                if (output[i] == null || output[i].Length < _blockSize)
                    throw new ArgumentException($"Output channel {i} must have at least {_blockSize} samples, but has {output[i]?.Length ?? 0}");
            }

            for (int i = 0; i < InputCount; i++)
            {
                Marshal.Copy(input[i], 0, _inputPtrs[i], _blockSize);
            }

            _pluginCommandStub.Commands.ProcessReplacing(_inputBuffers, _outputBuffers);

            for (int i = 0; i < OutputCount; i++)
            {
                Marshal.Copy(_outputPtrs[i], output[i], 0, _blockSize);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopProcessing();

                if (_inputPtrs != null)
                {
                    foreach (var ptr in _inputPtrs)
                        if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                }

                if (_outputPtrs != null)
                {
                    foreach (var ptr in _outputPtrs)
                        if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                }

                _disposed = true;
            }
        }
    }

    public class VstParameterInfo
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Display { get; set; }
        public string Label { get; set; }
        public float Value { get; set; }
    }
}