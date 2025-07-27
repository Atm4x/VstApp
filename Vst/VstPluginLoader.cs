using System;
using System.IO;
using Jacobi.Vst.Core.Host;
using Jacobi.Vst.Host.Interop;

namespace Teston.Vst
{
    public class VstPluginLoader : IVstPluginLoader
    {
        public VstPluginContext LoadPlugin(string vstPath, IVstHostCommandStub hostCommandStub)
        {
            if (!File.Exists(vstPath))
            {
                throw new FileNotFoundException($"VST плагин не найден: {vstPath}");
            }

            try
            {
                var context = VstPluginContext.Create(vstPath, hostCommandStub);
                context.PluginCommandStub.Commands.Open();
                return context;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Не удалось загрузить VST плагин: {vstPath}", ex);
            }
        }

        public void UnloadPlugin(VstPluginContext context)
        {
            if (context != null)
            {
                try
                {
                    context.PluginCommandStub.Commands.Close();
                    context.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при выгрузке плагина: {ex.Message}");
                }
            }
        }
    }
}