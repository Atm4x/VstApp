using Jacobi.Vst.Core.Host;
using Jacobi.Vst.Host.Interop;

namespace Teston.Vst
{
    public interface IVstPluginLoader
    {
        VstPluginContext LoadPlugin(string vstPath, IVstHostCommandStub hostCommandStub);
        void UnloadPlugin(VstPluginContext context);
    }
}