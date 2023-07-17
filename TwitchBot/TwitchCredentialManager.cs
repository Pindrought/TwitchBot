using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;

namespace TwitchBot
{
    internal static class TwitchCredentialManager
    {
        public static string UserId = "";
        public static string AuthToken = "";
        public static string ChannelName = "";
        public static bool Initialize()
        {
            if (File.Exists("config.ini") == false)
            {
                using(StreamWriter sw = new StreamWriter("config.ini"))
                {
                    sw.WriteLine("TwitchUserId=PUTYOURTWITCHNAMEHERE");
                    sw.WriteLine("TwitchAccessToken=PUTYOURACCESSTOKENHERE");
                    sw.WriteLine("ChannelName=PUTYOURCHANNELHERE");
                    sw.WriteLine("NOTE: YOUR TWITCH USER ID AND CHANNEL NAME WILL USUALLY BE THE SAME UNLESS YOU WANT A DEDICATED BOT USER");
                }
                MessageBox.Show("Please set up your config.ini file!");
                Environment.Exit(1);
            }
            using (StreamReader sr = new StreamReader("config.ini"))
            {
                string? line = sr.ReadLine();
                while(line != null)
                {
                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex != -1)
                    {
                        string key = line.Substring(0, equalsIndex).Trim();
                        string value = line.Substring(equalsIndex + 1).Trim();
                        if (key == "TwitchUserId")
                        {
                            UserId = value;
                        }
                        if (key == "TwitchAccessToken")
                        {
                            AuthToken = value;
                        }
                        if (key == "ChannelName")
                        {
                            ChannelName = value;
                            if (ChannelName.Contains("#") == false)
                            {
                                ChannelName = $"#{ChannelName}";
                            }
                        }
                    }
                    line = sr.ReadLine();
                }
            }
            if (UserId == "" || AuthToken == "" || ChannelName == "")
            {
                MessageBox.Show("Failed to load config.ini to pull twitch credentials for connecting to chat.");
                return false;
            }
            return true;
        }
    }
}
