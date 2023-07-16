using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace TwitchBot
{
    internal class TwitchMessageManager
    {
        private TwitchClient client;

        public delegate void OnMessageReceivedCallbackHandler(string user, string msg);
        public event OnMessageReceivedCallbackHandler OnMessageReceivedCallback;

        public TwitchMessageManager()
        {
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
            client = new TwitchClient(customClient);
            client.Initialize(credentials, "#Poopmaker");

            //client.OnLog += OnLog;
            //client.OnJoinedChannel += OnJoinedChannel;
            client.OnMessageReceived += OnMessageReceived;
            //client.OnWhisperReceived += OnWhisperReceived;
            //client.OnNewSubscriber += OnNewSubscriber;
            client.OnConnected += OnConnected;

            if (!client.Connect())
            {
                MessageBox.Show("Failed to connect to twitch! Fix your connection credentials.");
            }
        }

        private void OnConnected(object sender, OnConnectedArgs e)
        {
            Console.WriteLine($"Connected to {e.AutoJoinChannel}");
        }


        private void OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            string user = e.ChatMessage.DisplayName;
            var msg = e.ChatMessage.Message;
            if (OnMessageReceivedCallback != null)
            {
                OnMessageReceivedCallback(user, msg);
            }
        }
    }
}
