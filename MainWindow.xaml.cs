using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Teston.Vst;
using System.Linq;
using System.Collections.Generic;

namespace Teston
{
    public class MainWindow : Window
    {
        private VstChain _chain;
        private string _audioPath;
        private Task _playbackTask;

        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);
            _chain = new VstChain(1024, 44100f); // По умолчанию
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
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка добавления плагина: {ex.Message}");
                    await ShowMessageBox("Ошибка", $"Не удалось добавить плагин: {ex.Message}");
                }
            }
        }

        private async void SelectAudio_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filters = { new FileDialogFilter { Name = "WAV", Extensions = { "wav" } } } };
            var result = await dialog.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                _audioPath = result[0];
                using var reader = new NAudio.Wave.WaveFileReader(_audioPath);
                // НЕ пересоздаём _chain! Просто логируем
                Console.WriteLine($"Аудио выбрано: {_audioPath}, SampleRate: {reader.WaveFormat.SampleRate}. Плагинов в цепочке: {_chain.Managers.Count()}");
                this.FindControl<TextBlock>("txtStatus").Text = $"Выбран файл: {Path.GetFileName(_audioPath)}";
            }
        }

        private void Play_Click(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_audioPath))
            {
                _ = ShowMessageBox("Ошибка", "Выберите аудиофайл!");
                return;
            }

            Console.WriteLine("Нажата Play. Запуск воспроизведения...");

            _playbackTask = Task.Run(() =>
            {
                try
                {
                    Dispatcher.UIThread.InvokeAsync(() => this.FindControl<TextBlock>("txtStatus").Text = "Воспроизведение...").Wait();
                    _chain.ProcessAndPlay(_audioPath);
                    Dispatcher.UIThread.InvokeAsync(() => this.FindControl<TextBlock>("txtStatus").Text = "Готово").Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка в воспроизведении: {ex}");
                    Dispatcher.UIThread.InvokeAsync(async () => await ShowMessageBox("Ошибка", ex.Message)).Wait();
                }
            });
        }

        private void Stop_Click(object? sender, RoutedEventArgs e)
        {
            // TODO: Добавьте остановку (например, waveOut.Stop() из VstChain)
            Console.WriteLine("Нажата Stop.");
        }

        private void Chain_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var listBox = (ListBox)sender;
            if (listBox.SelectedIndex >= 0)
            {
                var managers = _chain.Managers.ToArray();
                var manager = managers[listBox.SelectedIndex];
                UpdateParametersPanel(manager);
            }
            else
            {
                ResetParametersPanel();
            }
        }

        private void UpdateChainList()
        {
            var listBox = this.FindControl<ListBox>("lstChain");
            listBox.ItemsSource = _chain.Managers.Select(m => m.PluginName).ToList(); // В 11.x ItemsSource лучше
        }

        private void UpdateParametersPanel(VstManager manager)
        {
            var panel = this.FindControl<StackPanel>("pnlParameters");
            panel.Children.Clear();

            // Показываем панель параметров и скрываем заглушку
            this.FindControl<Border>("pnlParametersContainer").IsVisible = true;
            this.FindControl<Border>("pnlNoSelection").IsVisible = false;

            // Обновляем заголовок
            this.FindControl<TextBlock>("txtPluginName").Text = $"Параметры: {manager.PluginName}";

            foreach (var param in manager.GetParameters().Values)
            {
                // Контейнер для параметра
                var paramContainer = new Border
                {
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D2D30")),
                    BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#3F3F46")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10)
                };

                var paramPanel = new StackPanel { Spacing = 5 };

                // Заголовок параметра
                var headerPanel = new DockPanel();
                var nameLabel = new TextBlock
                {
                    Text = param.Name,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC"))
                };
                var valueLabel = new TextBlock
                {
                    Text = $"{param.Display} {param.Label}",
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#569CD6"))
                };

                DockPanel.SetDock(valueLabel, Dock.Right);
                headerPanel.Children.Add(valueLabel);
                headerPanel.Children.Add(nameLabel);

                // Слайдер
                var slider = new Slider
                {
                    Minimum = 0,
                    Maximum = 1,
                    Value = param.Value,
                    TickFrequency = 0.1,
                    IsSnapToTickEnabled = false,
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#007ACC")),
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#3F3F46"))
                };

                slider.ValueChanged += (s, args) =>
                {
                    manager.SetParameter(param.Index, (float)args.NewValue);
                    // Обновляем отображение значения
                    valueLabel.Text = $"{manager.GetParameters()[param.Index].Display} {param.Label}";
                };

                paramPanel.Children.Add(headerPanel);
                paramPanel.Children.Add(slider);
                paramContainer.Child = paramPanel;

                panel.Children.Add(paramContainer);
            }

            // Кнопка открытия редактора
            var btnOpenEditor = new Button
            {
                Content = "Открыть редактор плагина",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 10, 0, 0),
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#007ACC")),
                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFFFFF"))
            };
            btnOpenEditor.Click += (s, args) => OpenVstEditorWindow(manager);
            panel.Children.Add(btnOpenEditor);
        }
        private void ResetParametersPanel()
        {
            var panel = this.FindControl<StackPanel>("pnlParameters");
            panel.Children.Clear();

            // Скрываем панель параметров и показываем заглушку
            this.FindControl<Border>("pnlParametersContainer").IsVisible = false;
            this.FindControl<Border>("pnlNoSelection").IsVisible = true;
        }

        private void OpenVstEditorWindow(VstManager manager)
        {
            var editorWindow = new Window
            {
                Title = $"Editor: {manager.PluginName}",
                Width = 400,
                Height = 300,
                CanResize = true
            };

            manager.OpenEditor(editorWindow.TryGetPlatformHandle().Handle); // В 11.x напрямую Handle.Handle

            editorWindow.ShowDialog(this);

            editorWindow.Closed += (sender, e) => manager.CloseEditor();
        }

        private async Task ShowMessageBox(string title, string text)
        {
            var msgBox = new Window { Title = title, Content = new TextBlock { Text = text }, Width = 300, Height = 150 };
            await msgBox.ShowDialog(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            _chain?.Dispose();
            base.OnClosed(e);
        }
    }
}