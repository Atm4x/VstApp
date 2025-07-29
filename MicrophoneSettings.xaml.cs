using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using NAudio.Wave;

namespace Teston
{
    public partial class MicrophoneSettings : Window
    {
        public int SelectedDevice => this.FindControl<ListBox>("lstDevices").SelectedIndex;

        public MicrophoneSettings()
        {
            InitializeComponent();
            LoadDevices();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void LoadDevices()
        {
            var lstDevices = this.FindControl<ListBox>("lstDevices");
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                lstDevices.Items.Add(WaveInEvent.GetCapabilities(i).ProductName);
            }
        }

        private void Ok_Click(object? sender, RoutedEventArgs e)
        {
            if (SelectedDevice >= 0)
            {
                Close();
            }
            else
            {
                // Можно добавить сообщение об ошибке
            }
        }
    }
}