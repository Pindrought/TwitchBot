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
        public static bool Initialize()
        {
            using (StreamReader sr = new StreamReader("config.ini"))
            {
                string line = sr.ReadLine();
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
                    }
                    line = sr.ReadLine();
                }
            }
            if (UserId == "" || AuthToken == "")
            {
                MessageBox.Show("Failed to load config.ini to pull twitch credentials for connecting to chat.");
                return false;
            }
            return true;
        }
    }
}
