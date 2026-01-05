using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using Vosk;

namespace SpeechSH
{
    class VoiceService : IDisposable
    {
        private readonly Action<string> _log;

        private WaveInEvent _waveIn;
        private Model _model;
        private VoskRecognizer _rec;
        private TtsService _tts = new TtsService();
        private bool _wakeDetected = false;
        private MQTT _MQTT;
        private string[] WAKE_WORD = { "hi baby", "hey baby", "my baby","xin chào"};

        public VoiceService(Action<string> log)
        {
            _log = log;
        }
        public void StartMQTT()
        {
            _MQTT = new MQTT(_log);
            _MQTT.ConnectMqtt();
        }

        #region Vosk
        public void StartVosk()
        {
            _log("VoiceService Start");

            Vosk.Vosk.SetLogLevel(0);
            _model = new Model("vosk-model-small-en-us-0.15");
            //_model = new Model("vosk-model-vn-0.4");
            _rec = new VoskRecognizer(_model, 16000);
            _rec.SetWords(true);

            StartRecordVosk();
        }
        private void StartRecordVosk()
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = GetSafeMicIndex(),
                WaveFormat = new WaveFormat(16000, 1),
                BufferMilliseconds = 200
            };

            _waveIn.DataAvailable += OnData;
            _waveIn.StartRecording();

            _log("Recording started");
        }
        private void StopRecordVosk()
        {
            _waveIn?.StopRecording();
            //_log("Recording stopped");
        }
        public void Dispose()
        {
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _rec?.Dispose();
            _model?.Dispose();
        }
        private void OnData(object sender, WaveInEventArgs e)
        {
            if (_rec.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                HandleText(_rec.Result());
            }
        }
        private void HandleText(string json)
        {
            var text = ExtractText(json);
            if (string.IsNullOrEmpty(text))
                return;

            _log("Heard: " + text);

            if (!_wakeDetected)
            {
                if (WAKE_WORD.Any(w=> text.Contains(w)))
                {
                    _wakeDetected = true;
                    StopRecordVosk();
                    _log("I'm here");
                    _tts.Speak("I'm here");
                    Thread.Sleep(500);
                    CallWhisper();
                }
            }
        }
        private string ExtractText(string json)
        {
            var key = "\"text\" : \"";
            var i = json.IndexOf(key);
            if (i < 0) return null;
            i += key.Length;
            var j = json.IndexOf("\"", i);
            return json.Substring(i, j - i);
        }
        private int GetSafeMicIndex()
        {
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                if (caps.Channels > 0)
                    return i;
            }
            throw new Exception("No microphone found");
        }

        private void CallWhisper()
        {
            Task.Run(() =>
            {
                _log("Starting Whisper...");
                StartRecord();
                Thread.Sleep(5000); // record 5 seconds
                StopRecord();
                while (!recordDone)
                {
                    Thread.Sleep(10);
                }
                string result = RunWhisper();
                if (string.IsNullOrEmpty(result))
                {
                    CallWhisper();
                }
                else
                {
                    HandleText2(result);
                    StartRecordVosk();
                    _wakeDetected = false;
                }
            });
        }
        #endregion

        #region whisper

        WaveInEvent waveIn;
        WaveFileWriter writer;
        private bool recordDone;
        private void StartRecord()
        {
            recordDone = false;
            waveIn = new WaveInEvent();
            waveIn.DeviceNumber = 0;
            waveIn.WaveFormat = new WaveFormat(16000, 1); // 16kHz mono
            waveIn.DataAvailable += (s, a) =>
            {
                writer.Write(a.Buffer, 0, a.BytesRecorded);
            };
            waveIn.RecordingStopped += (s, a) =>
            {
                writer.Dispose();
                waveIn.Dispose();
                recordDone = true;
            };

            writer = new WaveFileWriter(@"D:\Whisper\voice.wav", waveIn.WaveFormat);
            waveIn.StartRecording();
        }

        private void StopRecord()
        {
            waveIn.StopRecording();
        }

        private string RunWhisper()
        {
            var psi = new ProcessStartInfo
            {
                FileName = @"D:\Whisper\whisper-cli.exe",
                Arguments = "-m models\\ggml-small.bin -f voice.wav -l vi -otxt",
                WorkingDirectory = @"D:\Whisper",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            File.Delete("D:\\Whisper\\voice.wav.txt");
            var p = Process.Start(psi);
            p.WaitForExit();
            if (File.Exists("D:\\Whisper\\voice.wav.txt") == false)
            {
                return "";
            }
            else
            { return File.ReadAllText("D:\\Whisper\\voice.wav.txt"); }
        }

        private void HandleText2(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            text = text.ToLower().Trim();


            _MQTT.SendRelayCommand(0, true);
            _log("Command: " + text);
        }
        #endregion

        public class TtsService
        {
            private SpeechSynthesizer _tts;
            public TtsService()
            {
                _tts = new SpeechSynthesizer();
                _tts.Rate = 0;
                _tts.Volume = 100;
            }

            public void Speak(string text)
            {
                _tts.SpeakAsync(text);
            }
        }
    }
}
