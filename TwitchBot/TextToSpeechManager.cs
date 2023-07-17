using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Schema;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix;
using TwitchLib.Communication.Interfaces;

namespace TwitchBot
{
    internal class TextToSpeechManager
    {
        private readonly object queuedSoundsMemoryLock = new object();
        private Queue<MemoryStream> queuedSounds = new Queue<MemoryStream>();
        private HttpClient httpClient = new HttpClient();
        private Thread soundPlayingThread;
        private volatile bool shutdownInitiated = false;
        private Dictionary<string, string> voices_streamelements = new Dictionary<string, string>();
        private Dictionary<string, string> voices_tiktok = new Dictionary<string, string>();

        public TextToSpeechManager()
        {
            using (StreamReader sr = new StreamReader("voices_streamelements.txt"))
            {
                string line = sr.ReadLine();
                while (line != null)
                {
                    line = line.Trim();
                    if (!line.Equals(""))
                    {
                        voices_streamelements.Add(line.ToLower(), line);
                    }
                    line = sr.ReadLine();
                }
            }

            using (StreamReader sr = new StreamReader("voices_tiktok.txt"))
            {
                string line = sr.ReadLine();
                while (line != null)
                {
                    line = line.Trim();
                    if (!line.Equals("") && !line.Contains("#"))
                    {
                        if (line.Contains("="))
                        {
                            string[] secs = line.Split('=');
                            voices_tiktok.Add(secs[0].ToLower(), secs[1]);
                        }
                    }
                    line = sr.ReadLine();
                }
            }

            soundPlayingThread = new Thread(() =>
            {
                while(true)
                {
                    if (shutdownInitiated == true)
                    {
                        return;
                    }
                    Thread.Sleep(10);
                    if (Monitor.TryEnter(queuedSoundsMemoryLock))
                    {
                        try
                        {
                            if (queuedSounds.Count > 0)
                            {
                                MemoryStream soundStream = queuedSounds.Dequeue();
                                Monitor.Exit(queuedSoundsMemoryLock);


                                using (var reader = new NAudio.Wave.Mp3FileReader(soundStream))
                                {
                                    using (var waveOut = new WaveOut())
                                    {
                                        waveOut.Init(reader);
                                        waveOut.Volume = 1.0f;
                                        waveOut.Play();

                                        while (waveOut.PlaybackState == PlaybackState.Playing)
                                        {
                                            Thread.Sleep(10);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Monitor.Exit(queuedSoundsMemoryLock);
                            }
                        }
                        catch (Exception ex) 
                        {
                            Console.WriteLine(ex.ToString());
                            Monitor.Exit(queuedSoundsMemoryLock);
                        }
                    }
                }
            });
            soundPlayingThread.Start();
        }

        public bool IsVoiceValid(string voice)
        {
            if (voices_tiktok.ContainsKey(voice))
            {
                return true;
            }
            if (voices_streamelements.ContainsKey(voice))
            {
                return true;
            }
            return false;
        }

        public Dictionary<string, string> GetVoices()
        {
            Dictionary<string, string> allVoices = new Dictionary<string, string>();
            foreach(var voice in voices_tiktok)
            {
                allVoices.Add(voice.Key, voice.Value);
            }
            foreach (var voice in voices_streamelements)
            {
                allVoices.Add(voice.Key, voice.Value);
            }
            //StreamWriter sw = new StreamWriter("allvoices.txt");
            //foreach(var v in allVoices)
            //{
            //    sw.WriteLine(v.Key);
            //}
            //sw.Close();
            return allVoices;
        }

        public void RemoveVoice(string voice)
        {
            //voices_streamelements.Remove(voice);
            //using (StreamWriter sw = new StreamWriter("voices.txt"))
            //{
            //    foreach(var v in voices_streamelements)
            //    {
            //        sw.WriteLine(v.ToString());
            //    }
            //}
        }

        public void Shutdown()
        {
            lock (queuedSoundsMemoryLock)
            {
                shutdownInitiated = true;
            }
        }

        public async void AddTextRequest(string txt, string voice)
        {
            if (voices_tiktok.ContainsKey(voice))
            {
                //TikTok api impl
                {
                    voice = voices_tiktok.First(k => k.Key == voice).Value;
                    var text = txt;

                    var voiceText = new { voice = voice, text = text };

                    HttpContent content = new StringContent(JsonConvert.SerializeObject(voiceText));
                    content.Headers.Remove("Content-Type");
                    content.Headers.Add("Content-Type", MediaTypeNames.Application.Json);

                    var json = await httpClient.PostAsync("https://tiktok-tts.weilnet.workers.dev/api/generation",
                                                          content);

                    if (json.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        return;
                    }

                    var response = await json.Content.ReadAsStringAsync();
                    var deserializedResponse = JsonConvert.DeserializeObject<JObject>(response);

                    var binaryData = Convert.FromBase64String(deserializedResponse["data"].ToString());

                    MemoryStream soundStream = new MemoryStream(binaryData);
                    Monitor.Enter(queuedSoundsMemoryLock);
                    queuedSounds.Enqueue(soundStream);
                    Monitor.Exit(queuedSoundsMemoryLock);
                }
            }
            //StreamElements impl
            if (voices_streamelements.ContainsKey(voice))
            {
                voice = voices_streamelements.First(k => k.Key == voice).Value;

                var text = Uri.EscapeDataString(txt);

                var json = await httpClient.GetAsync($"https://api.streamelements.com/kappa/v2/speech?voice={voice}&text={text}");

                if (json.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    MessageBox.Show("Bad Status Code? Voice = " + voice + "\n" + json.StatusCode.ToString() + "\nRequest: " + json.ToString());
                    return;
                }

                var data = await json.Content.ReadAsByteArrayAsync();
                MemoryStream soundStream = new MemoryStream(data.ToArray());
                Monitor.Enter(queuedSoundsMemoryLock);
                queuedSounds.Enqueue(soundStream);
                Monitor.Exit(queuedSoundsMemoryLock);
            }
        }

    }
}
