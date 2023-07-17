using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using static System.Net.Mime.MediaTypeNames;

namespace TwitchBot
{
    internal class TwitchMessageManager
    {
        private TwitchClient _client;
        private TextToSpeechManager _textToSpeechManager;

        public delegate void OnMessageReceivedCallbackHandler(string user, string msg);
        public event OnMessageReceivedCallbackHandler OnMessageReceivedCallback;

        public delegate void OnVoiceChangedCallbackHandler(string user, string voice);
        public event OnVoiceChangedCallbackHandler OnVoiceChangedCallback;
        Stopwatch _timeSinceLastVoiceURLSent = null;

        public TwitchMessageManager(TextToSpeechManager textToSpeechManager)
        {
            _textToSpeechManager = textToSpeechManager;

            if (!TwitchCredentialManager.Initialize())
            {
                Environment.Exit(-1);
            }

            ConnectionCredentials credentials = new ConnectionCredentials(TwitchCredentialManager.UserId, TwitchCredentialManager.AuthToken);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            _client = new TwitchClient(customClient);
            _client.Initialize(credentials, TwitchCredentialManager.ChannelName);

            _client.OnMessageReceived += OnMessageReceived;
            _client.OnNewSubscriber += OnNewSubscriber;
            _client.OnConnected += OnConnected;

            if (!_client.Connect())
            {
                MessageBox.Show("Failed to connect to twitch! Fix your connection credentials in your config.ini.");
            }
        }

        private void OnConnected(object sender, OnConnectedArgs e)
        {
            Console.WriteLine($"Connected to {e.AutoJoinChannel}");
        }


        private void OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {

        }


        private void OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            string user = e.ChatMessage.DisplayName;
            var msg = e.ChatMessage.Message;
            if (msg.ToLower().StartsWith("!voice"))
            {
                if (msg.Length > 7)
                {
                    msg = msg.ToLower();
                    string voice = msg.Substring(6).Trim();
                    if (_textToSpeechManager.IsVoiceValid(voice))
                    {
                        if (OnVoiceChangedCallback != null)
                        {
                            OnVoiceChangedCallback(user, voice);
                        }
                    }
                    else
                    {
                        if (_timeSinceLastVoiceURLSent == null)
                        {
                            _client.SendMessage(TwitchCredentialManager.ChannelName, $"@{e.ChatMessage.DisplayName} Invalid voice specified. See URL for list of voices. https://pastebin.com/cZMn4SzT");
                            _timeSinceLastVoiceURLSent = new Stopwatch();
                            _timeSinceLastVoiceURLSent.Start();
                        }
                        else
                        {
                            if (_timeSinceLastVoiceURLSent.ElapsedMilliseconds > 5000)
                            {
                                _client.SendMessage(TwitchCredentialManager.ChannelName, $"@{e.ChatMessage.DisplayName} Invalid voice specified. See URL for list of voices. https://pastebin.com/cZMn4SzT");
                                _timeSinceLastVoiceURLSent.Restart();
                            }
                        }

                        //Whispers aren't working i'm not sure why and don't care enough to fix it at the moment.
                        //_client.SendWhisper(e.ChatMessage.Username, "Invalid voice specified. See URL for list of voices. https://pastebin.com/cZMn4SzT");
                    }
                }
                else //Improper usage
                {
                    if (_timeSinceLastVoiceURLSent == null)
                    {
                        _client.SendMessage(TwitchCredentialManager.ChannelName, $"@{e.ChatMessage.DisplayName} Invalid voice specified. See URL for list of voices. https://pastebin.com/cZMn4SzT");
                        _timeSinceLastVoiceURLSent = new Stopwatch();
                        _timeSinceLastVoiceURLSent.Start();
                    }
                    else
                    {
                        if (_timeSinceLastVoiceURLSent.ElapsedMilliseconds > 5000)
                        {
                            _client.SendMessage(TwitchCredentialManager.ChannelName, $"@{e.ChatMessage.DisplayName} Invalid voice specified. See URL for list of voices. https://pastebin.com/cZMn4SzT");
                            _timeSinceLastVoiceURLSent.Restart();
                        }
                    }
                }
            }
            else
            {
                string cleanedText = Regex.Replace(msg, @"http[^\s]+", ""); //no url aids spam
                if (OnMessageReceivedCallback != null)
                {
                    OnMessageReceivedCallback(user, cleanedText);
                }
            }
        }
    }
}
