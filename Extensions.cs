using Avalonia.Controls;
using Avalonia.Platform;
using System;
using System.Reflection;

namespace Teston
{
    public static class WindowExtensions
    {
        public static IntPtr GetPlatformHandle(this Window window)
        {
            var platformImpl = window.PlatformImpl;
            var handleProperty = platformImpl.GetType().GetProperty("Handle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (handleProperty != null)
            {
                var handleObj = handleProperty.GetValue(platformImpl);
                if (handleObj is IPlatformHandle platformHandle)
                {
                    return platformHandle.Handle; // Теперь извлекаем IntPtr из IPlatformHandle
                }
            }
            throw new InvalidOperationException("Не удалось получить Handle окна.");
        }
    }
}