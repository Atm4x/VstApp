using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Jacobi.Vst.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Teston.Vst;
using NAudio.Wave;

namespace Teston
{
    public class PluginItem
    {
        public string Name { get; set; }
        public bool IsEnabled { get; set; }
        public VstManager Manager { get; set; }
    }

    public class MainWindow : Window
    {
        private VstChain _chain;
        private string _audioPath;
        private int _micDevice = -1;
        private Task _playbackTask;
        private WaveOutEvent _waveOut;
        private BufferedWaveProvider _waveProvider;
        private BufferedWaveProvider _inputBuffered;
        private WaveStream _audioReader;
        private WaveInEvent _waveIn;
        private ISampleProvider _sourceProvider;
        private ISampleProvider _currentProcessedProvider;
        private CancellationTokenSource _playbackCts;
        private readonly object _providerLock = new object();
        private readonly int _blockSize = 1024;
        private bool _isMicMode;
        private bool _isPaused;

        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);
            _chain = new VstChain(_blockSize, 44100f); // По умолчанию
        }

        private async void AddPlugin_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filters = { new FileDialogFilter { Name = "VST DLL", Extensions = { "dll" } } } };
            var result = await dialog.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                try
                {
                    _chain.AddPlugin(result[0]);
                    UpdateChainList();
                    Console.WriteLine($"Плагин успешно добавлен. Всего в цепочке: {_chain.Managers.Count()}");

                    // Динамическое обновление цепочки, если воспроизведение идёт
                    if (_currentProcessedProvider != null)
                    {
                        RefreshProcessedProvider();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка добавления плагина: {ex.Message}");
                    await ShowMessageBox("Ошибка", $"Не удалось добавить плагин: {ex.Message}");
                }
            }
        }

        private async void SelectMic_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new MicrophoneSettings();
            await dialog.ShowDialog(this);
            var selected = dialog.SelectedDevice;
            if (selected >= 0)
            {
                _micDevice = selected;
                _audioPath = null;
                this.FindControl<TextBlock>("txtStatus").Text = $"Выбран микрофон: {WaveInEvent.GetCapabilities(selected).ProductName}";
            }
        }

        private async void SelectAudio_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filters = { new FileDialogFilter { Name = "Audio Files", Extensions = { "wav", "mp3", "flac", "ogg" } } } };
            var result = await dialog.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                _audioPath = result[0];
                _micDevice = -1;
                try
                {
                    var waveFormat = VstChain.GetAudioFormat(_audioPath);
                    Console.WriteLine($"Аудио выбрано: {_audioPath}, SampleRate: {waveFormat.SampleRate}. Плагинов в цепочке: {_chain.Managers.Count()}");
                    this.FindControl<TextBlock>("txtStatus").Text = $"Выбран файл: {Path.GetFileName(_audioPath)}";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка чтения формата: {ex.Message}");
                    await ShowMessageBox("Ошибка", $"Не удалось прочитать файл: {ex.Message}");
                }
            }
        }

        private async void Play_Click(object? sender, RoutedEventArgs e)
        {
            if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Paused)
            {
                _waveOut.Play();
                _isPaused = false;
                this.FindControl<TextBlock>("txtStatus").Text = "Воспроизведение...";
                this.FindControl<Button>("btnPause").Content = "Pause";
                return;
            }

            if (string.IsNullOrEmpty(_audioPath) && _micDevice < 0)
            {
                await ShowMessageBox("Ошибка", "Выберите аудиофайл или микрофон!");
                return;
            }

            try
            {
                _isPaused = false;
                if (!string.IsNullOrEmpty(_audioPath))
                {
                    _isMicMode = false;
                    _audioReader = VstChain.CreateAudioReader(_audioPath);
                    _sourceProvider = _audioReader.ToSampleProvider();
                }
                else
                {
                    _isMicMode = true;
                    _waveIn = new WaveInEvent { DeviceNumber = _micDevice };
                    _waveIn.WaveFormat = new WaveFormat(44100, 16, 2); // Соответствует SampleRate в VstChain
                    _inputBuffered = new BufferedWaveProvider(_waveIn.WaveFormat);
                    _waveIn.DataAvailable += WaveIn_DataAvailable;
                    _sourceProvider = _inputBuffered.ToSampleProvider();
                    _waveIn.StartRecording();
                    this.FindControl<TextBlock>("txtStatus").Text = "Прослушивание микрофона...";
                }

                _currentProcessedProvider = _chain.CreateChainedProvider(_sourceProvider);
                var outputFormat = _currentProcessedProvider.WaveFormat;
                _waveProvider = new BufferedWaveProvider(outputFormat);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_waveProvider);
                _waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
                _waveOut.Play();
                _playbackCts = new CancellationTokenSource();
                _playbackTask = Task.Run(() => PlaybackLoop(_playbackCts.Token));
                this.FindControl<Button>("btnPause").Content = "Pause";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в воспроизведении: {ex}");
                await ShowMessageBox("Ошибка", ex.Message);
            }
        }


        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (!_isPaused)
            {
                _inputBuffered.AddSamples(e.Buffer, 0, e.BytesRecorded);
            }
        }

        private void Pause_Click(object? sender, RoutedEventArgs e)
        {
            if (_waveOut == null) return;

            var btn = this.FindControl<Button>("btnPause");
            if (_waveOut.PlaybackState == PlaybackState.Playing)
            {
                _waveOut.Pause();
                _isPaused = true;
                btn.Content = "Resume";
                this.FindControl<TextBlock>("txtStatus").Text = "Пауза";
            }
            else if (_waveOut.PlaybackState == PlaybackState.Paused)
            {
                _waveOut.Play();
                _isPaused = false;
                btn.Content = "Pause";
                this.FindControl<TextBlock>("txtStatus").Text = _isMicMode ? "Прослушивание микрофона..." : "Воспроизведение...";
            }
        }

        private void FullStop_Click(object? sender, RoutedEventArgs e)
        {
            if (_waveOut != null)
            {
                _waveOut.Stop(); // Вызовет PlaybackStopped
            }
        }

        private void WaveOut_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.FindControl<TextBlock>("txtStatus").Text = "Готово";
                CleanupPlayback();
            });
        }

        private void CleanupPlayback()
        {
            _playbackCts?.Cancel();
            _waveOut?.Dispose();
            _waveOut = null;
            _audioReader?.Dispose();
            _audioReader = null;
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
                _waveIn.Dispose();
                _waveIn = null;
            }
            _inputBuffered = null;
            _currentProcessedProvider = null;
            _waveProvider = null;
            _playbackCts = null;
            _isPaused = false;
            _isMicMode = false;
            this.FindControl<Button>("btnPause").Content = "Pause";
        }

        private void PlaybackLoop(CancellationToken token)
        {
            if (_waveProvider == null) return;

            int channels = _waveProvider.WaveFormat.Channels;
            float[] floatBuffer = new float[_blockSize * channels];
            byte[] byteBuffer = new byte[floatBuffer.Length * 4];

            while (!token.IsCancellationRequested)
            {
                if (_isPaused)
                {
                    Thread.Sleep(50);
                    continue;
                }

                int samplesRead;
                lock (_providerLock)
                {
                    if (_currentProcessedProvider == null) break;
                    samplesRead = _currentProcessedProvider.Read(floatBuffer, 0, floatBuffer.Length);
                }

                if (samplesRead == 0)
                {
                    if (!_isMicMode) break;
                    Thread.Sleep(10);
                    continue;
                }

                Buffer.BlockCopy(floatBuffer, 0, byteBuffer, 0, samplesRead * 4);
                _waveProvider.AddSamples(byteBuffer, 0, samplesRead * 4);

                while (_waveProvider.BufferedDuration.TotalSeconds > 0.3 && !token.IsCancellationRequested)
                {
                    Thread.Sleep(50);
                }
            }
        }

        private void Chain_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var listBox = (ListBox)sender;
            if (listBox.SelectedItem is PluginItem selected)
            {
                UpdateParametersPanel(selected.Manager);
            }
            else
            {
                ResetParametersPanel();
            }
        }

        private void PluginEnabled_Changed(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is PluginItem item)
            {
                item.Manager.IsEnabled = checkBox.IsChecked ?? false;
                Console.WriteLine($"Плагин '{item.Name}' {(item.Manager.IsEnabled ? "включён" : "выключен")}");
                // Нет нужды в refresh, так как IsEnabled влияет напрямую на ProcessAudio
            }
        }

        private void Up_Click(object? sender, RoutedEventArgs e)
        {
            var listBox = this.FindControl<ListBox>("lstChain");
            int index = listBox.SelectedIndex;
            if (index > 0)
            {
                _chain.MovePlugin(index, index - 1);
                UpdateChainList();
                listBox.SelectedIndex = index - 1;
                if (_waveOut?.PlaybackState == PlaybackState.Playing)
                {
                    RefreshProcessedProvider();
                }
            }
        }

        private void Down_Click(object? sender, RoutedEventArgs e)
        {
            var listBox = this.FindControl<ListBox>("lstChain");
            int index = listBox.SelectedIndex;
            if (index >= 0 && index < listBox.ItemCount - 1)
            {
                _chain.MovePlugin(index, index + 1);
                UpdateChainList();
                listBox.SelectedIndex = index + 1;
                if (_waveOut?.PlaybackState == PlaybackState.Playing)
                {
                    RefreshProcessedProvider();
                }
            }
        }

        private void RefreshProcessedProvider()
        {
            lock (_providerLock)
            {
                _currentProcessedProvider = _chain.CreateChainedProvider(_sourceProvider);
            }
            Console.WriteLine("Цепочка обработки обновлена динамически.");
        }

        private void UpdateChainList()
        {
            var listBox = this.FindControl<ListBox>("lstChain");
            listBox.ItemsSource = _chain.Managers.Select(m => new PluginItem
            {
                Name = m.PluginName,
                IsEnabled = m.IsEnabled,
                Manager = m
            }).ToList();
        }

        private void UpdateParametersPanel(VstManager manager)
        {
            var panel = this.FindControl<StackPanel>("pnlParameters");
            panel.Children.Clear();

            this.FindControl<Border>("pnlParametersContainer").IsVisible = true;
            this.FindControl<Border>("pnlNoSelection").IsVisible = false;

            this.FindControl<TextBlock>("txtPluginName").Text = manager.PluginName;

            foreach (var param in manager.GetParameters().Values)
            {
                var paramContainer = new StackPanel { Spacing = 8 };

                var headerPanel = new DockPanel();
                var nameLabel = new TextBlock
                {
                    Text = param.Name,
                    Classes = { "param-name" }
                };
                var valueLabel = new TextBlock
                {
                    Text = $"{param.Display} {param.Label}",
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Classes = { "param-value" }
                };

                DockPanel.SetDock(valueLabel, Dock.Right);
                headerPanel.Children.Add(valueLabel);
                headerPanel.Children.Add(nameLabel);

                var slider = new Slider
                {
                    Minimum = 0,
                    Maximum = 1,
                    Value = param.Value,
                    Height = 6,
                    CornerRadius = new CornerRadius(3)
                };

                slider.ValueChanged += (s, args) =>
                {
                    manager.SetParameter(param.Index, (float)args.NewValue);
                    valueLabel.Text = $"{manager.GetParameters()[param.Index].Display} {param.Label}";
                };

                paramContainer.Children.Add(headerPanel);
                paramContainer.Children.Add(slider);

                panel.Children.Add(paramContainer);
            }

            var btnOpenEditor = new Button
            {
                Content = "Открыть редактор плагина",
                Classes = { "primary" },
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 20, 0, 0)
            };
            btnOpenEditor.Click += (s, args) => OpenVstEditorWindow(manager);
            panel.Children.Add(btnOpenEditor);
        }

        private void ResetParametersPanel()
        {
            var panel = this.FindControl<StackPanel>("pnlParameters");
            panel.Children.Clear();

            this.FindControl<Border>("pnlParametersContainer").IsVisible = false;
            this.FindControl<Border>("pnlNoSelection").IsVisible = true;
        }

        private void OpenVstEditorWindow(VstManager mgr)
        {
            if ((mgr._pluginInfo.Flags & VstPluginFlags.HasEditor) == 0)
            {
                _ = ShowMessageBox("Инфо", "Этот плагин не имеет графического интерфейса.");
                return;
            }

            var wnd = new Window
            {
                Title = $"Editor: {mgr.PluginName}",
                Width = 700,
                Height = 500
            };

            var host = new VstEditorHost();
            wnd.Content = host;

            var idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            idleTimer.Tick += (_, __) => mgr.EditorIdle();

            wnd.Opened += (_, __) =>
            {
                if (host.ParentHwnd == IntPtr.Zero)
                {
                    Console.WriteLine("HWND не получен!");
                    return;
                }

                Console.WriteLine($"Открываем GUI {mgr.PluginName}, hwnd={host.ParentHwnd}");
                mgr.OpenEditor(host.ParentHwnd);
                idleTimer.Start();
            };

            wnd.Closed += (_, __) =>
            {
                idleTimer.Stop();
                mgr.CloseEditor();
            };

            wnd.ShowDialog(this);
        }

        private async Task ShowMessageBox(string title, string text)
        {
            var msgBox = new Window { Title = title, Content = new TextBlock { Text = text }, Width = 300, Height = 150 };
            await msgBox.ShowDialog(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            CleanupPlayback();
            _chain?.Dispose();
            base.OnClosed(e);
        }
    }
}