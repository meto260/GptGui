using GptGui.Models;
using GptGui.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace GptGui {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private readonly OllamaService _ollamaService;
        private readonly ObservableCollection<ChatSession> _sessions;
        private ChatSession _currentSession;

        public MainWindow() {
            InitializeComponent();
            _ollamaService = new OllamaService();
            _sessions = new ObservableCollection<ChatSession>();
            SessionList.ItemsSource = _sessions;

            LoadModels();
        }

        private async void LoadModels() {
            try {
                var models = await _ollamaService.GetAvailableModels();
                ModelSelector.ItemsSource = models;
                if (models.Any())
                    ModelSelector.SelectedIndex = 0;
            }
            catch (Exception ex) {
                MessageBox.Show($"Failed to load models: {ex.Message}");
            }
        }

        private void NewChat_Click(object sender, RoutedEventArgs e) {
            var selectedModel = ModelSelector.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedModel))
                return;

            var session = new ChatSession {
                ModelName = selectedModel,
                SessionName = "New Chat" // Varsayılan başlık
            };

            _sessions.Add(session);
            SessionList.SelectedItem = session;
        }

        private void SessionList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            _currentSession = SessionList.SelectedItem as ChatSession;
            if (_currentSession != null) {
                ChatMessages.ItemsSource = _currentSession.Messages;
            }
        }

        private async void MessageInput_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(MessageInput.Text)) {
                if (_currentSession == null)
                    return;

                var userMessage = new ChatMessage {
                    Role = "user",
                    Content = MessageInput.Text
                };
                _currentSession.Messages.Add(userMessage);

                string userInput = MessageInput.Text;
                MessageInput.Text = "";

                try {
                    LoadingIndicator.Visibility = Visibility.Visible;

                    var response = await _ollamaService.GetCompletion(
                        _currentSession.ModelName,
                        userInput,
                        _currentSession.Messages.ToList());

                    var assistantMessage = new ChatMessage {
                        Role = "assistant",
                        Content = response
                    };
                    _currentSession.Messages.Add(assistantMessage);

                    // Asistanın ilk cevabını aldıktan sonra, "aşağıdaki mesajın başlığı ne olur? Lütfen boşluklar dahil 30 karakteri geçmeyecek kısa bir başlık verin." sorusunu sor
                    string titlePrompt = $"Aşağıdaki mesajın başlığı ne olur? Lütfen boşluklar dahil 30 karakteri geçmeyecek kısa bir başlık verin.\n\n{userInput}";
                    var titleResponse = await _ollamaService.GetCompletion(
                        _currentSession.ModelName,
                        titlePrompt,
                        _currentSession.Messages.ToList());

                    // Asistanın verdiği cevabı kullanarak SessionName'i güncelle
                    string sessionName = GenerateSessionName(titleResponse);
                    _currentSession.SessionName = sessionName;
                    SessionList.Items.Refresh(); // ListView'ı yenile

                    await Task.Delay(100);
                    ChatScroller.ScrollToBottom();
                }
                catch (Exception ex) {
                    MessageBox.Show($"Error: {ex.Message}");
                }
                finally {
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) {
            string searchText = SearchBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText)) {
                foreach (ChatMessage message in _currentSession?.Messages ?? Enumerable.Empty<ChatMessage>()) {
                    message.IsVisible = true;
                }
            } else {
                foreach (ChatMessage message in _currentSession?.Messages ?? Enumerable.Empty<ChatMessage>()) {
                    message.IsVisible = message.Content.ToLower().Contains(searchText) ||
                                      message.Role.ToLower().Contains(searchText);
                }
            }

            ChatMessages.Items.Refresh();
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e) {
            SearchBox.Clear();
        }

        private async void UploadFile_Click(object sender, RoutedEventArgs e) {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            if (openFileDialog.ShowDialog() == true) {
                var fileContent = await File.ReadAllBytesAsync(openFileDialog.FileName);
                var fileName = System.IO.Path.GetFileName(openFileDialog.FileName);

                if (_currentSession == null)
                    return;

                var userMessage = new ChatMessage {
                    Role = "user",
                    Content = MessageInput.Text,
                    FileContent = fileContent,
                    FileName = fileName
                };
                _currentSession.Messages.Add(userMessage);

                try {
                    LoadingIndicator.Visibility = Visibility.Visible;

                    var response = await _ollamaService.GetCompletionWithFile(
                        _currentSession.ModelName,
                        MessageInput.Text,
                        fileContent,
                        fileName,
                        _currentSession.Messages.ToList());

                    _currentSession.Messages.Add(new ChatMessage {
                        Role = "assistant",
                        Content = response
                    });

                    MessageInput.Text = "";
                    await Task.Delay(100);
                    ChatScroller.ScrollToBottom();
                }
                catch (Exception ex) {
                    MessageBox.Show($"Error: {ex.Message}");
                }
                finally {
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void CopyMessage_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.Tag is ChatMessage message) {
                Clipboard.SetText(message.Content);
            }
        }

        private void CopyCode_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.Tag is string code) {
                Clipboard.SetText(code);
            }
        }

        private string GenerateSessionName(string content) {
            // İçeriğin ilk 30 karakterini alarak bir başlık oluştur
            string sessionName = content.Length > 30 ? content.Substring(0, 30) : content;
            return sessionName;
        }
    }


    public class BooleanToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is Visibility visibility && visibility == Visibility.Visible;
        }
    }

    public class InverseBooleanConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is bool boolValue)
                return !boolValue;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is bool boolValue)
                return !boolValue;
            return value;
        }
    }
    public class RoleToColorConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            string role = value as string;
            return role == "user" ? new SolidColorBrush(Color.FromRgb(0, 122, 204))
                                 : new SolidColorBrush(Color.FromRgb(45, 45, 45));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

}