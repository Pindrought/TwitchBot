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
        Stopwatch? _timeSinceLastVoiceURLSent = null;
        const double _minDurationBetweenVoiceURLNotification = 5000; //5000 Miliseconds = 5 seconds

        public delegate void OnMessageReceivedCallbackHandler(string user, string msg);
        public event OnMessageReceivedCallbackHandler OnMessageReceivedCallback;

        public delegate void OnVoiceChangedCallbackHandler(string user, string voice);
        public event OnVoiceChangedCallbackHandler OnVoiceChangedCallback;

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

            _client.AddChatCommandIdentifier('!');
            _client.OnConnected += OnConnected;
            _client.OnNewSubscriber += OnNewSubscriber;
            _client.OnMessageReceived += OnMessageReceived;
            _client.OnChatCommandReceived += OnChatCommandReceived;

            if (!_client.Connect())
            {
                MessageBox.Show("Failed to connect to twitch! Fix your connection credentials in your config.ini.");
            }
        }

        private void OnConnected(object? sender, OnConnectedArgs e)
        {
        }

        private void OnNewSubscriber(object? sender, OnNewSubscriberArgs e)
        {
        }

        private void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            string user = e.ChatMessage.DisplayName;
            var msg = e.ChatMessage.Message;
            if (msg.StartsWith("!") == false) //Make sure we are ignoring commands
            {                
                string cleanedText = Regex.Replace(msg, @"http[^\s]+", ""); //no url aids spam
                if (OnMessageReceivedCallback != null)
                {
                    OnMessageReceivedCallback(user, cleanedText);
                }
            }
        }

        private void OnChatCommandReceived(object? sender, OnChatCommandReceivedArgs e)
        {
            var args = e.Command.ArgumentsAsList;
            var user = e.Command.ChatMessage.DisplayName;
            var cmd = e.Command.CommandText.ToLower();
            var msg = e.Command.ChatMessage.Message;

            switch (cmd)
            {
                case "voice":
                    {
                        if (args.Count > 0)
                        {
                            var fullVoiceText = args[0].ToLower();
                            for(int i=1; i<args.Count; i++)
                            {
                                fullVoiceText += " " + args[i].ToLower();
                            }

                            if (_textToSpeechManager.IsVoiceValid(fullVoiceText))
                            {
                                if (OnVoiceChangedCallback != null)
                                {
                                    OnVoiceChangedCallback(user, fullVoiceText);
                                }
                            }
                            else
                            {
                                //Whispers aren't working i'm not sure why and don't care enough to fix it, so i'll just send chat messages to everyone for now.
                                //_client.SendWhisper(e.ChatMessage.Username, "Invalid voice specified. See URL for list of voices. https://pastebin.com/cZMn4SzT");
                                if (_timeSinceLastVoiceURLSent == null) //If first time sending the notification about how to use !voice
                                {
                                    _client.SendMessage(TwitchCredentialManager.ChannelName, $"@{user} Invalid voice specified. See URL for list of voices. https://pastebin.com/cZMn4SzT");
                                    _timeSinceLastVoiceURLSent = new Stopwatch();
                                    _timeSinceLastVoiceURLSent.Start();
                                }
                                else
                                {
                                    if (_timeSinceLastVoiceURLSent.ElapsedMilliseconds > _minDurationBetweenVoiceURLNotification)
                                    {
                                        _client.SendMessage(TwitchCredentialManager.ChannelName, $"@{user} Invalid voice specified. See URL for list of voices. https://pastebin.com/cZMn4SzT");
                                        _timeSinceLastVoiceURLSent.Restart();
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (_timeSinceLastVoiceURLSent == null)
                            {
                                _client.SendMessage(TwitchCredentialManager.ChannelName, $"@{user} Invalid voice specified. See URL for list of voices. https://pastebin.com/cZMn4SzT");
                                _timeSinceLastVoiceURLSent = new Stopwatch();
                                _timeSinceLastVoiceURLSent.Start();
                            }
                            else
                            {
                                if (_timeSinceLastVoiceURLSent.ElapsedMilliseconds > _minDurationBetweenVoiceURLNotification)
                                {
                                    _client.SendMessage(TwitchCredentialManager.ChannelName, $"@{user} Invalid voice specified. See URL for list of voices. https://pastebin.com/cZMn4SzT");
                                    _timeSinceLastVoiceURLSent.Restart();
                                }
                            }
                        }
                        break;
                    }
            }
        }
    }
}
