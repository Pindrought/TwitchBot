using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Schema;
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
        private List<String> voices = new List<string>();
        public TextToSpeechManager()
        {
            using (StreamReader sr = new StreamReader("voices.txt"))
            {
                string line = sr.ReadLine();
                while (line != null)
                {
                    line = line.Trim();
                    if (!line.Equals(""))
                    {
                        voices.Add(line);
                    }
                    line = sr.ReadLine();
                }
            }
            voices.Sort();

            //Copied/pasted this lol idk what it does will check later might not even need it
            //httpClient.DefaultRequestHeaders.Accept.Clear();
            //httpClient.DefaultRequestHeaders.Accept.Add(
            //    new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            //httpClient.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");
            //httpClient.DefaultRequestHeaders.Add()
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");

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

        public List<String> GetVoices()
        {
            return voices;
        }

        public void RemoveVoice(string voice)
        {
            voices.Remove(voice);
            using (StreamWriter sw = new StreamWriter("voices.txt"))
            {
                foreach(var v in voices)
                {
                    sw.WriteLine(v.ToString());
                }
            }
        }

        public void Shutdown()
        {
            lock (queuedSoundsMemoryLock)
            {
                shutdownInitiated = true;
            }
        }

        public class VoiceTextObject
        {
            public string voice;
            public string text;
        }

        public async void AddTextRequest(string txt, string voice)
        {
            var text = Uri.EscapeDataString(txt);

            if (!voices.Contains(voice))
            {
                return;
            }


            //TikTok api impl
            {
                voice = "en_us_chewbacca";
                //var requestText = $"https://tiktok-tts.weilnet.workers.dev/api/generation&voice={voice}&text={text}";

                text = "hello";

                VoiceTextObject vto = new VoiceTextObject();
                vto.voice = voice;
                vto.text = text;

                //var serializedJson = JsonConvert.SerializeObject(vto);

                var json = await httpClient.PostAsync("https://tiktok-tts.weilnet.workers.dev/api/generation",
                                                      new StringContent(JsonConvert.SerializeObject(vto), Encoding.UTF8, MediaTypeNames.Application.Json));

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

            //StreamElements impl
            //{
            //    var json = await httpClient.GetAsync("https://api.streamelements.com/kappa/v2/speech?voice=" + voice + "&text=" + text);

            //    if (json.StatusCode != System.Net.HttpStatusCode.OK)
            //    {
            //        MessageBox.Show("Bad Status Code? Voice = " + voice + "\n" + json.StatusCode.ToString() + "\nRequest: " + json.ToString());
            //        return;
            //    }

            //    var data = await json.Content.ReadAsByteArrayAsync();
            //    MemoryStream soundStream = new MemoryStream(data.ToArray());
            //    Monitor.Enter(queuedSoundsMemoryLock);
            //    queuedSounds.Enqueue(soundStream);
            //    Monitor.Exit(queuedSoundsMemoryLock);
            //}

        }



    }
}
