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
        private readonly object _queuedSoundsMemoryLock = new object();
        private Queue<MemoryStream> _queuedSounds = new Queue<MemoryStream>();
        private HttpClient _httpClient = new HttpClient();
        private Thread _soundPlayingThread;
        private volatile bool _shutdownInitiated = false;
        private Dictionary<string, string> _voices_streamelements = new Dictionary<string, string>();
        private Dictionary<string, string> _voices_tiktok = new Dictionary<string, string>();
        ThreadSafeBool _skipCurrentSound = new ThreadSafeBool();

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
                        _voices_streamelements.Add(line.ToLower(), line);
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
                            _voices_tiktok.Add(secs[0].ToLower(), secs[1]);
                        }
                    }
                    line = sr.ReadLine();
                }
            }

            _soundPlayingThread = new Thread(() =>
            {
                while(true)
                {
                    if (_shutdownInitiated == true)
                    {
                        return;
                    }
                    Thread.Sleep(10);
                    if (Monitor.TryEnter(_queuedSoundsMemoryLock))
                    {
                        try
                        {
                            if (_queuedSounds.Count > 0)
                            {
                                MemoryStream soundStream = _queuedSounds.Dequeue();
                                Monitor.Exit(_queuedSoundsMemoryLock);


                                using (var reader = new NAudio.Wave.Mp3FileReader(soundStream))
                                {
                                    using (var waveOut = new WaveOut())
                                    {
                                        waveOut.Init(reader);
                                        waveOut.Volume = 1.0f;
                                        waveOut.Play();

                                        if (_skipCurrentSound.Value == true)
                                        {
                                            _skipCurrentSound.Value = false;
                                        }

                                        while (waveOut.PlaybackState == PlaybackState.Playing)
                                        {
                                            Thread.Sleep(10);
                                            if (_skipCurrentSound.Value == true)
                                            {
                                                _skipCurrentSound.Value = false;
                                                waveOut.Stop();
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Monitor.Exit(_queuedSoundsMemoryLock);
                            }
                        }
                        catch (Exception ex) 
                        {
                            Console.WriteLine(ex.ToString());
                            Monitor.Exit(_queuedSoundsMemoryLock);
                        }
                    }
                }
            });
            _soundPlayingThread.Start();
        }


        public void SkipCurrentSound()
        {
            _skipCurrentSound.Value = true;
        }

        public bool IsVoiceValid(string voice)
        {
            if (_voices_tiktok.ContainsKey(voice))
            {
                return true;
            }
            if (_voices_streamelements.ContainsKey(voice))
            {
                return true;
            }
            return false;
        }

        public Dictionary<string, string> GetVoices()
        {
            Dictionary<string, string> allVoices = new Dictionary<string, string>();
            foreach(var voice in _voices_tiktok)
            {
                allVoices.Add(voice.Key, voice.Value);
            }
            foreach (var voice in _voices_streamelements)
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
            Monitor.Enter(_queuedSoundsMemoryLock);
            _shutdownInitiated = true;
            Monitor.Exit(_queuedSoundsMemoryLock);
        }

        public async void AddTextRequest(string txt, string voice)
        {
            if (_voices_tiktok.ContainsKey(voice))
            {
                //TikTok api impl
                {
                    voice = _voices_tiktok.First(k => k.Key == voice).Value;
                    var text = txt;

                    var voiceText = new { voice = voice, text = text };

                    HttpContent content = new StringContent(JsonConvert.SerializeObject(voiceText));
                    content.Headers.Remove("Content-Type");
                    content.Headers.Add("Content-Type", MediaTypeNames.Application.Json);

                    var json = await _httpClient.PostAsync("https://tiktok-tts.weilnet.workers.dev/api/generation",
                                                          content);

                    if (json.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        return;
                    }

                    var response = await json.Content.ReadAsStringAsync();
                    var deserializedResponse = JsonConvert.DeserializeObject<JObject>(response);

                    var binaryData = Convert.FromBase64String(deserializedResponse["data"].ToString());

                    MemoryStream soundStream = new MemoryStream(binaryData);
                    Monitor.Enter(_queuedSoundsMemoryLock);
                    _queuedSounds.Enqueue(soundStream);
                    Monitor.Exit(_queuedSoundsMemoryLock);
                }
            }
            //StreamElements impl
            if (_voices_streamelements.ContainsKey(voice))
            {
                voice = _voices_streamelements.First(k => k.Key == voice).Value;

                var text = Uri.EscapeDataString(txt);

                var json = await _httpClient.GetAsync($"https://api.streamelements.com/kappa/v2/speech?voice={voice}&text={text}");

                if (json.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    MessageBox.Show("Bad Status Code? Voice = " + voice + "\n" + json.StatusCode.ToString() + "\nRequest: " + json.ToString());
                    return;
                }

                var data = await json.Content.ReadAsByteArrayAsync();
                MemoryStream soundStream = new MemoryStream(data.ToArray());
                Monitor.Enter(_queuedSoundsMemoryLock);
                _queuedSounds.Enqueue(soundStream);
                Monitor.Exit(_queuedSoundsMemoryLock);
            }
        }

    }
}
