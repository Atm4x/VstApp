using Avalonia.Controls;
using Avalonia.Platform;

namespace Teston.Vst
{
    public sealed class VstEditorHost : NativeControlHost
    {
        public IntPtr ParentHwnd { get; private set; } = IntPtr.Zero;

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            // Avalonia даёт HWND родителя — его и запоминаем
            ParentHwnd = parent?.Handle ?? IntPtr.Zero;

            // Возвращаем фиктивный дочерний хэндл: плагин сам создаст окно
            return new PlatformHandle(IntPtr.Zero, parent?.HandleDescriptor ?? "HWND");
        }
    }
}
