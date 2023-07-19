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
        private Dictionary<string, string> _existingUserToVoiceDictionaryLookup = new Dictionary<string, string>();

        Random _rng = new Random(); //Random number generator used for when a voice is randomly picked for a new user
        List<string> _mutedUsers = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
            _twitchMessageManager = new TwitchMessageManager(_textToSpeechManager);
            _twitchMessageManager.OnMessageReceivedCallback += AddMessageToBeProcessed;
            _twitchMessageManager.OnVoiceChangedCallback += AssignVoice;

            foreach(var voice in _textToSpeechManager.GetVoices())
            {
                ComboBox_Voice.Items.Add(voice);
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

            if (File.Exists("user_assigned_voices.txt"))
            {
                using (StreamReader sr = new StreamReader("user_assigned_voices.txt"))
                {
                    string line = sr.ReadLine();
                    while (line != null)
                    {
                        if (!line.Equals(""))
                        {
                            var sections = line.Split('=');
                            var username = sections[0];
                            var voice = sections[1];
                            _existingUserToVoiceDictionaryLookup[username] = voice;
                        }
                        line = sr.ReadLine();
                    }
                }
            }

            //_textToSpeechManager.DumpVoicesToFile("list of voices.txt");
            //_textToSpeechManager.DumpSoundsToFile("list of sounds.txt");
        }

        private void SaveUpdatedUserAssignedVoicesData()
        {
            using (StreamWriter sw = new StreamWriter("user_assigned_voices.txt"))
            {
                foreach(var userToVoicePair in _existingUserToVoiceDictionaryLookup)
                {
                    sw.WriteLine($"{userToVoicePair.Key}={userToVoicePair.Value}"); //Ex. Pindrought=chewbacca
                }
            }
        }

        private readonly object _threadSafety_Fnc_AssignOrGetVoiceForUser = new object();
        private string AssignOrGetVoiceForUser(string user, string? assignedVoice)
        {
            lock (_threadSafety_Fnc_AssignOrGetVoiceForUser)
            {
                //If we are specifying a voice to assign, do the following...
                if (assignedVoice != null)
                {
                    _existingUserToVoiceDictionaryLookup[user] = assignedVoice;
                    _userToVoiceDictionaryLookup[user] = assignedVoice;
                    SaveUpdatedUserAssignedVoicesData();
                    var dataGridEntry = _dataGridCollection.FirstOrDefault(x => x.UserId == user);
                    if (dataGridEntry == null)
                    {
                        bool muted = _mutedUsers.Contains(user);
                        //Need to invoke the dispatcher since technically this function is being called with a callback from a different thread
                        //and we cannot update the datagrid from a different thread's context
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
                        //Need to invoke the dispatcher since technically this function is being called with a callback from a different thread
                        //and we cannot update the datagrid from a different thread's context
                        Dispatcher.Invoke(new Action(() =>
                        {
                            dataGridEntry.Voice = assignedVoice;
                            DataGrid_UsersAndVoices.Items.Refresh();
                        }));
                    }
                    return assignedVoice;
                }

                //When just getting the voice, we pass in null for assignedVoice
                //Alternatively, if there is no existing voice for the user, a random voice will be selected
                string voice;
                if (_userToVoiceDictionaryLookup.TryGetValue(user, out voice)) //Key exists in our active user->voices dictionary already so we will do nothing and just return the voice
                {
                    return voice;
                }
                else
                {
                    string selectedVoice = "";
                    if (_existingUserToVoiceDictionaryLookup.ContainsKey(user)) //Is this saved from a previous session? If so pull the saved voice
                    {
                        selectedVoice = _existingUserToVoiceDictionaryLookup[user];
                    }
                    else //If not saved from a previous session, we need to generate a new voice
                    {
                        int index = _rng.Next(0, ComboBox_Voice.Items.Count);
                        selectedVoice = ComboBox_Voice.Items[index].ToString(); //Select a random voice from the available voices - could change this to get the voices from textToSpeechManager
                                                                                //but that is pretty slow since it's building a new dictionary based on the two different voice sets from the two
                                                                                //different API's
                        _existingUserToVoiceDictionaryLookup[user] = selectedVoice;
                        SaveUpdatedUserAssignedVoicesData();
                    }

                    _userToVoiceDictionaryLookup[user] = selectedVoice;
                    voice = selectedVoice;
                    var dataGridEntry = _dataGridCollection.FirstOrDefault(x => x.UserId == user);
                    if (dataGridEntry == null) //This should always be null at this point
                    {
                        bool muted = _mutedUsers.Contains(user);
                        //Need to invoke the dispatcher since technically this function is being called with a callback from a different thread
                        //and we cannot update the datagrid from a different thread's context
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

                    return voice;
                }
            }
        }

        private void AssignVoice(string user, string voice)
        {
            AssignOrGetVoiceForUser(user, voice); //Since the voice is specified, this will be assigning the user to the voice parm.
        }

        private void AddMessageToBeProcessed(string user,string msg)
        {
            if (_mutedUsers.Contains(user))
                return;

            string voice = AssignOrGetVoiceForUser(user, null);

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

        private void Button_NewVoice_Click(object sender, RoutedEventArgs e) //Assign a new voice to the selected user.
        {
            var rowModel = (sender as Button).DataContext as DataGridModel;
            if (rowModel == null)
                return;

            _userToVoiceDictionaryLookup.Remove(rowModel.UserId);
            rowModel.Voice = AssignOrGetVoiceForUser(rowModel.UserId, null);
            _existingUserToVoiceDictionaryLookup[rowModel.UserId] = rowModel.Voice;
            SaveUpdatedUserAssignedVoicesData();
            DataGrid_UsersAndVoices.Items.Refresh();
        }

        void OnMuteChecked(object sender, RoutedEventArgs e)
        {
            var cb = sender as DataGridCell;
            var rowModel = cb.DataContext as DataGridModel;
            if (_mutedUsers.Contains(rowModel.UserId) == false)
            {
                //Updating the file for all of our muted users
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
                //Updating the file for all of our muted users
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
