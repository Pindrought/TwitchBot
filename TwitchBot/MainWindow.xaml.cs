using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Web;
using System.IO;
using static System.Net.Mime.MediaTypeNames;
using System.Collections.ObjectModel;
using NAudio.CoreAudioApi;

namespace TwitchBot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TextToSpeechManager _textToSpeechManager = new TextToSpeechManager();
        TwitchMessageManager _twitchMessageManager = null;
        ObservableCollection<DataGridModel> _dataGridCollection = new ObservableCollection<DataGridModel>();
        private Dictionary<string, string> _userToVoiceDictionaryLookup = new Dictionary<string, string>();
        Random _rng = new Random();
        List<string> _mutedUsers = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
            _twitchMessageManager = new TwitchMessageManager(_textToSpeechManager);
            _twitchMessageManager.OnMessageReceivedCallback += AddMessageToBeProcessed;
            _twitchMessageManager.OnVoiceChangedCallback += AssignVoice;

            foreach(var voice in _textToSpeechManager.GetVoices())
            {
                ComboBox_Voice.Items.Add(voice.Key);
            }
            if (ComboBox_Voice.Items.Count > 0) 
            {
                ComboBox_Voice.SelectedIndex = 0;
            }

            DataGrid_UsersAndVoices.ItemsSource = _dataGridCollection;

            if (File.Exists("muted.txt"))
            {
                using (StreamReader sr = new StreamReader("muted.txt"))
                {
                    string line = sr.ReadLine();
                    while(line != null)
                    {
                        if (!line.Equals(""))
                            _mutedUsers.Add(line);
                        line = sr.ReadLine();
                    }
                }
            }
        }

        private string AssignOrGetVoiceForUser(string user, string? assignedVoice)
        {
            if (assignedVoice != null) 
            {
                if (_userToVoiceDictionaryLookup.ContainsKey(user))
                {
                    _userToVoiceDictionaryLookup[user] = assignedVoice;
                }
                else
                {
                    _userToVoiceDictionaryLookup.Add(user, assignedVoice);
                }
                var dataGridEntry = _dataGridCollection.FirstOrDefault(x => x.UserId == user);
                if (dataGridEntry == null)
                {
                    bool muted = _mutedUsers.Contains(user);

                    Dispatcher.Invoke(new Action(() =>
                    {
                        _dataGridCollection.Add(new DataGridModel()
                        {
                            UserId = user,
                            Voice = assignedVoice,
                            Muted = muted
                        });
                        
                        DataGrid_UsersAndVoices.Items.Refresh();
                    }));
                }
                else
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        dataGridEntry.Voice = assignedVoice;
                        DataGrid_UsersAndVoices.Items.Refresh();
                    }));
                }
                return assignedVoice;
            }

            string voice;
            if (_userToVoiceDictionaryLookup.TryGetValue(user, out voice)) //Key exists in dictionary
            {

            }
            else //need to add key to dictionary
            {
                int index = _rng.Next(0, ComboBox_Voice.Items.Count);
                string selectedVoice = ComboBox_Voice.Items[index].ToString();
                _userToVoiceDictionaryLookup.Add(user, selectedVoice);
                voice = selectedVoice;
                var dataGridEntry = _dataGridCollection.FirstOrDefault(x => x.UserId == user);
                if (dataGridEntry == null)
                {
                    bool muted = _mutedUsers.Contains(user);

                    Dispatcher.Invoke(new Action(() =>
                    {
                        _dataGridCollection.Add(new DataGridModel()
                        {
                            UserId = user,
                            Voice = voice,
                            Muted = muted
                        });
                        DataGrid_UsersAndVoices.Items.Refresh();
                    }));
                }
            }
            return voice;
        }

        private void AssignVoice(string user, string voice)
        {
            AssignOrGetVoiceForUser(user, voice);
        }

        private void AddMessageToBeProcessed(string user,string msg)
        {
            string voice = AssignOrGetVoiceForUser(user, null);

            if (_mutedUsers.Contains(user))
                return;

            _textToSpeechManager.AddTextRequest(msg, voice);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var text = TextBox_TextToSpeech.Text;
            _textToSpeechManager.AddTextRequest(text, ComboBox_Voice.SelectedItem.ToString());
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _textToSpeechManager.Shutdown();
        }

        private void Button_RemoveVoice_Click(object sender, RoutedEventArgs e)
        {
            var rowModel = (sender as Button).DataContext as DataGridModel;
            if (rowModel == null)
                return;

            var result = MessageBox.Show("Are you sure you want to permanently remove this voice?", "Voice Removal Confirmation", MessageBoxButton.YesNoCancel);
            if (result != MessageBoxResult.Yes) 
            {
                return;
            }

            _textToSpeechManager.RemoveVoice(rowModel.Voice);

            foreach(var row in _dataGridCollection)
            {
                if (row.Voice == rowModel.Voice)
                {
                    _userToVoiceDictionaryLookup.Remove(row.UserId);
                    row.Voice = AssignOrGetVoiceForUser(row.UserId, null);
                }
            }

            DataGrid_UsersAndVoices.Items.Refresh();
        }

        private void Button_NewVoice_Click(object sender, RoutedEventArgs e)
        {
            var rowModel = (sender as Button).DataContext as DataGridModel;
            if (rowModel == null)
                return;

            _userToVoiceDictionaryLookup.Remove(rowModel.UserId);
            rowModel.Voice = AssignOrGetVoiceForUser(rowModel.UserId, null);

            DataGrid_UsersAndVoices.Items.Refresh();
        }

        void OnMuteChecked(object sender, RoutedEventArgs e)
        {
            var cb = sender as DataGridCell;
            var rowModel = cb.DataContext as DataGridModel;

            if (_mutedUsers.Contains(rowModel.UserId) == false)
            {
                _mutedUsers.Add(rowModel.UserId);
                using (StreamWriter sw = new StreamWriter("muted.txt"))
                {
                    foreach (var user in _mutedUsers)
                    {
                        sw.WriteLine(user);
                    }
                }
            }
        }

        void OnMuteUnchecked(object sender, RoutedEventArgs e)
        {
            var cb = sender as DataGridCell;
            var rowModel = cb.DataContext as DataGridModel;

            if (_mutedUsers.Contains(rowModel.UserId) == true)
            {
                _mutedUsers.Remove(rowModel.UserId);
                using (StreamWriter sw = new StreamWriter("muted.txt"))
                {
                    foreach (var user in _mutedUsers)
                    {
                        sw.WriteLine(user);
                    }
                }
            }
        }

        private void Button_SkipSound(object sender, RoutedEventArgs e)
        {
            _textToSpeechManager.SkipCurrentSound();
        }
    }
}
