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
using static System.Net.Mime.MediaTypeNames;

namespace TwitchBot
{
    internal class TextToSpeechManager
    {
        private readonly object _queuedSoundsMemoryLock = new object();
        private Queue<List<MemoryStream>> _queuedSounds = new Queue<List<MemoryStream>>();
        private HttpClient _httpClient = new HttpClient();
        private Thread _soundPlayingThread;
        private volatile bool _shutdownInitiated = false;
        private Dictionary<string, string> _voices_streamelements = new Dictionary<string, string>();
        private Dictionary<string, string> _voices_tiktok = new Dictionary<string, string>();
        private Dictionary<string, MemoryStream> _soundEffects = new Dictionary<string, MemoryStream>();
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

            var soundFiles = Directory.GetFiles("sounds");
            foreach(var fullFile in soundFiles)
            {
                var fileNameShort = fullFile.ToLower().Substring(fullFile.LastIndexOf('\\') + 1);
                if (fileNameShort.EndsWith(".mp3"))
                {
                    fileNameShort = fileNameShort.Substring(0, fileNameShort.Length - 4); //remove the .mp3
                    byte[] bytes = File.ReadAllBytes(fullFile);
                    MemoryStream ms = new MemoryStream(bytes);
                    _soundEffects.Add("-" + fileNameShort, ms);
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
                                List<MemoryStream> soundStreamList = _queuedSounds.Dequeue();
                                Monitor.Exit(_queuedSoundsMemoryLock);

                                if (_skipCurrentSound.Value == true)
                                {
                                    _skipCurrentSound.Value = false;
                                }

                                for (int i = 0; i < soundStreamList.Count; i++)
                                { 
                                    var soundStream = soundStreamList[i];
                                    soundStream.Position = 0;
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
                                                if (_skipCurrentSound.Value == true)
                                                {
                                                    _skipCurrentSound.Value = false;
                                                    waveOut.Stop();
                                                    soundStreamList.Clear();
                                                }
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
            return allVoices;
        }

        public void DumpVoicesToFile(string fileName)
        {
            using (StreamWriter sw = new StreamWriter(fileName))
            {
                foreach (var v in _voices_tiktok)
                {
                    sw.WriteLine(v.Key);
                }
                foreach (var v in _voices_streamelements)
                {
                    sw.WriteLine(v.Key);
                }
            }
        }

        public void DumpSoundsToFile(string fileName)
        {
            using (StreamWriter sw = new StreamWriter(fileName))
            {
                foreach (var v in _soundEffects)
                {
                    sw.WriteLine(v.Key.Substring(1)); //When I load these in, I am prefixing them with a dash for simpler processing, but I want to remove the dash when writing out the sound names to a file
                }
            }
        }

        public void Shutdown()
        {
            Monitor.Enter(_queuedSoundsMemoryLock);
            _shutdownInitiated = true;
            Monitor.Exit(_queuedSoundsMemoryLock);
        }

        public async void AddTextRequest(string txt, string voice)
        {
            var words = txt.Split(' ');
            List<string> sections = new List<string>();
            string currentSection = "";
            foreach (var word in words)
            {
                
                if (_soundEffects.ContainsKey(word))
                {
                    if (currentSection != "")
                    {
                        sections.Add(currentSection);
                        currentSection = "";
                    }
                    sections.Add(word);
                }
                else
                {
                    if (currentSection == "")
                    {
                        currentSection = word;
                    }
                    else
                    {
                        currentSection += " " + word;
                    }
                }
            }
            if (currentSection != "")
            {
                sections.Add(currentSection);
            }

            List<MemoryStream> soundStreamList = new List<MemoryStream>();


            if (_voices_tiktok.ContainsKey(voice))
            {
                //TikTok api impl
                {
                    voice = _voices_tiktok.First(k => k.Key == voice).Value;
                    foreach (var sec in sections)
                    {
                        if (_soundEffects.ContainsKey(sec))
                        {
                            soundStreamList.Add(_soundEffects[sec]);
                        }
                        else
                        {
                            var voiceText = new { voice = voice, text = sec };

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
                            soundStreamList.Add(soundStream);
                        }
                    }
                }
            }
            //StreamElements impl
            if (_voices_streamelements.ContainsKey(voice))
            {
                voice = _voices_streamelements.First(k => k.Key == voice).Value;

                foreach(var sec in sections)
                {
                    if (_soundEffects.ContainsKey(sec))
                    {
                        soundStreamList.Add(_soundEffects[sec]);
                    }
                    else
                    {
                        var text = Uri.EscapeDataString(sec);

                        var json = await _httpClient.GetAsync($"https://api.streamelements.com/kappa/v2/speech?voice={voice}&text={text}");

                        if (json.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            MessageBox.Show("Bad Status Code? Voice = " + voice + "\n" + json.StatusCode.ToString() + "\nRequest: " + json.ToString());
                            return;
                        }

                        var data = await json.Content.ReadAsByteArrayAsync();
                        MemoryStream soundStream = new MemoryStream(data);
                        soundStreamList.Add(soundStream);
                    }
                }
            }

            Monitor.Enter(_queuedSoundsMemoryLock);
            _queuedSounds.Enqueue(soundStreamList);
            Monitor.Exit(_queuedSoundsMemoryLock);
        }
    }
}
