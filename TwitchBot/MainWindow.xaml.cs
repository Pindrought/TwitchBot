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
        TextToSpeechManager textToSpeechManager = new TextToSpeechManager();
        TwitchMessageManager twitchMessageManager = new TwitchMessageManager();
        ObservableCollection<DataGridModel> dataGridCollection = new ObservableCollection<DataGridModel>();
        private Dictionary<string, string> userToVoiceDictionaryLookup = new Dictionary<string, string>();
        Random rng = new Random();

        public MainWindow()
        {
            InitializeComponent();
            twitchMessageManager.OnMessageReceivedCallback += AddMessageToBeProcessed;
            foreach(var voice in textToSpeechManager.GetVoices())
            {
                ComboBox_Voice.Items.Add(voice);
            }
            if (ComboBox_Voice.Items.Count > 0) 
            {
                ComboBox_Voice.SelectedIndex = 0;
            }

            dataGridCollection.Add(new DataGridModel() { UserId = "test", Voice = "voice" });
            DataGrid_UsersAndVoices.ItemsSource = dataGridCollection;
        }

        private string AssignOrGetVoiceForUser(string user)
        {
            string voice;
            if (userToVoiceDictionaryLookup.TryGetValue(user, out voice)) //Key exists in dictionary
            {

            }
            else //need to add key to dictionary
            {
                int index = rng.Next(0, ComboBox_Voice.Items.Count);
                string selectedVoice = ComboBox_Voice.Items[index].ToString();
                userToVoiceDictionaryLookup.Add(user, selectedVoice);
                voice = selectedVoice;
                var dataGridEntry = dataGridCollection.FirstOrDefault(x => x.UserId == user);
                if (dataGridEntry == null)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        dataGridCollection.Add(new DataGridModel()
                        {
                            UserId = user,
                            Voice = voice,
                        });
                        DataGrid_UsersAndVoices.Items.Refresh();
                    }));
                }
            }
            return voice;
        }

        private void AddMessageToBeProcessed(string user,string msg)
        {
            string voice = AssignOrGetVoiceForUser(user);
            textToSpeechManager.AddTextRequest(msg, voice);
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var text = TextBox_TextToSpeech.Text;
            textToSpeechManager.AddTextRequest(text, ComboBox_Voice.SelectedItem.ToString());
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            textToSpeechManager.Shutdown();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2)
            {
                ComboBox_Voice.SelectedIndex += 1;
                var text = TextBox_TextToSpeech.Text;
                textToSpeechManager.AddTextRequest(text, ComboBox_Voice.SelectedItem.ToString());
            }
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

            textToSpeechManager.RemoveVoice(rowModel.Voice);

            foreach(var row in dataGridCollection)
            {
                if (row.Voice == rowModel.Voice)
                {
                    userToVoiceDictionaryLookup.Remove(row.UserId);
                    row.Voice = AssignOrGetVoiceForUser(row.UserId);
                }
            }

            DataGrid_UsersAndVoices.Items.Refresh();
        }

        private void Button_NewVoice_Click(object sender, RoutedEventArgs e)
        {
            var rowModel = (sender as Button).DataContext as DataGridModel;
            if (rowModel == null)
                return;

            userToVoiceDictionaryLookup.Remove(rowModel.UserId);
            rowModel.Voice = AssignOrGetVoiceForUser(rowModel.UserId);

            DataGrid_UsersAndVoices.Items.Refresh();
        }
    }
}
