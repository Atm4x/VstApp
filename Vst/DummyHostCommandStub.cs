using System;
using Jacobi.Vst.Core;
using Jacobi.Vst.Core.Host;

namespace Teston.Vst
{
    internal sealed class DummyHostCommandStub : IVstHostCommandStub
    {
        public event EventHandler<PluginCalledEventArgs> PluginCalled;
        public event EventHandler<SizeWindowEventArgs> SizeWindow;
        public IVstPluginContext PluginContext { get; set; }
        public IVstHostCommands20 Commands { get; private set; }
        public float SampleRate { get; set; }
        public int BlockSize { get; set; }

        public DummyHostCommandStub()
        {
            Commands = new DummyHostCommands(this);
        }

        private void RaisePluginCalled(string message) => PluginCalled?.Invoke(this, new PluginCalledEventArgs(message));
        private void RaiseSizeWindow(int width, int height) => SizeWindow?.Invoke(this, new SizeWindowEventArgs(width, height));

        private sealed class DummyHostCommands : IVstHostCommands20
        {
            private readonly DummyHostCommandStub _cmdStub;
            public DummyHostCommands(DummyHostCommandStub cmdStub) { _cmdStub = cmdStub; }
            public int GetBlockSize() { _cmdStub.RaisePluginCalled("GetBlockSize()"); return _cmdStub.BlockSize; }
            public float GetSampleRate() { _cmdStub.RaisePluginCalled("GetSampleRate()"); return _cmdStub.SampleRate; }
            public int GetCurrentPluginID() { _cmdStub.RaisePluginCalled("GetCurrentPluginID()"); return _cmdStub.PluginContext?.PluginInfo.PluginID ?? 0; }

            public bool BeginEdit(int index) { _cmdStub.RaisePluginCalled($"BeginEdit({index})"); return false; }
            public VstCanDoResult CanDo(string cando) { _cmdStub.RaisePluginCalled($"CanDo({cando})"); return VstCanDoResult.Unknown; }
            public bool CloseFileSelector(VstFileSelect fileSelect) { _cmdStub.RaisePluginCalled($"CloseFileSelector({fileSelect.Command})"); return false; }
            public bool EndEdit(int index) { _cmdStub.RaisePluginCalled($"EndEdit({index})"); return false; }
            public VstAutomationStates GetAutomationState() { _cmdStub.RaisePluginCalled("GetAutomationState()"); return VstAutomationStates.Off; }
            public string GetDirectory() { _cmdStub.RaisePluginCalled("GetDirectory()"); return null; }
            public int GetInputLatency() { _cmdStub.RaisePluginCalled("GetInputLatency()"); return 0; }
            public VstHostLanguage GetLanguage() { _cmdStub.RaisePluginCalled("GetLanguage()"); return VstHostLanguage.NotSupported; }
            public int GetOutputLatency() { _cmdStub.RaisePluginCalled("GetOutputLatency()"); return 0; }
            public VstProcessLevels GetProcessLevel() { _cmdStub.RaisePluginCalled("GetProcessLevel()"); return VstProcessLevels.Realtime; }
            public string GetProductString() { _cmdStub.RaisePluginCalled("GetProductString()"); return "Teston Host"; }
            public VstTimeInfo GetTimeInfo(VstTimeInfoFlags filterFlags) { _cmdStub.RaisePluginCalled($"GetTimeInfo({filterFlags})"); return null; }
            public string GetVendorString() { _cmdStub.RaisePluginCalled("GetVendorString()"); return "VST.NET User"; }
            public int GetVendorVersion() { _cmdStub.RaisePluginCalled("GetVendorVersion()"); return 1; }
            public bool IoChanged() { _cmdStub.RaisePluginCalled("IoChanged()"); return false; }
            public bool OpenFileSelector(VstFileSelect fileSelect) { _cmdStub.RaisePluginCalled($"OpenFileSelector({fileSelect.Command})"); return false; }
            public bool ProcessEvents(VstEvent[] events) { _cmdStub.RaisePluginCalled($"ProcessEvents({events.Length})"); return false; }
            public bool SizeWindow(int width, int height) { _cmdStub.RaisePluginCalled($"SizeWindow({width}, {height})"); _cmdStub.RaiseSizeWindow(width, height); return true; }
            public bool UpdateDisplay() { _cmdStub.RaisePluginCalled("UpdateDisplay()"); return false; }
            public int GetVersion() { _cmdStub.RaisePluginCalled("GetVersion()"); return 2400; }
            public void ProcessIdle() { _cmdStub.RaisePluginCalled("ProcessIdle()"); }
            public void SetParameterAutomated(int index, float value) { _cmdStub.RaisePluginCalled($"SetParameterAutomated({index}, {value})"); }
        }
    }

    internal sealed class PluginCalledEventArgs : EventArgs
    {
        public string Message { get; }
        public PluginCalledEventArgs(string message) { Message = message; }
    }

    internal sealed class SizeWindowEventArgs : EventArgs
    {
        public int Width { get; }
        public int Height { get; }
        public SizeWindowEventArgs(int width, int height) { Width = width; Height = height; }
    }
}