using NAudio.Wave;
using System;
using System.Linq;
using System.Speech.Synthesis;
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

        private const string WAKE_WORD = "my name";

        public VoiceService(Action<string> log)
        {
            _log = log;
        }

        public void Start()
        {
            _log("VoiceService Start");

            Vosk.Vosk.SetLogLevel(0);
            //_model = new Model("vosk-model-small-en-us-0.15");
            _model = new Model("vosk-model-vn-0.4");
            _rec = new VoskRecognizer(_model, 16000);
            _rec.SetWords(true);

            _waveIn = new WaveInEvent
            {
                DeviceNumber = GetSafeMicIndex(),
                WaveFormat = new WaveFormat(16000, 1),
                BufferMilliseconds = 200
            };

            _waveIn.DataAvailable += OnData;
            _waveIn.StartRecording();

            _log("Recording started");


            _MQTT = new MQTT(_log);
            _MQTT.ConnectMqtt();
        }

        private void OnData(object sender, WaveInEventArgs e)
        {
            if (_rec.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                HandleText(_rec.Result());
                //_log("Bytes: " + e.BytesRecorded);
            }
            else
            {
                //HandlePartial(_rec.PartialResult());
                //_log("Bytes: " + e.BytesRecorded);
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
                if (text.Contains(WAKE_WORD))
                {
                    _wakeDetected = true;
                    _log("Yes sir");
                    _tts.Speak("Yes sir");
                }
                return;
            }

            // xử lý lệnh sau wake
            _MQTT.SendRelayCommand(0, true);

            _log("Command: " + text);
            _wakeDetected = false;
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
        private void HandlePartial(string json)
        {
            var text = Extract(json, "partial");
            if (!string.IsNullOrEmpty(text))
                _log("~ " + text);
        }
        private string Extract(string json, string key)
        {
            var k = $"\"{key}\" : \"";
            var i = json.IndexOf(k);
            if (i < 0) return null;
            i += k.Length;
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

        public void Stop()
        {
            _waveIn?.StopRecording();
        }

        public void Dispose()
        {
            _waveIn?.Dispose();
            _rec?.Dispose();
            _model?.Dispose();
        }


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
