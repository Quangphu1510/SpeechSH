using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vosk;

namespace SpeechSH
{
    public partial class Form1 : Form
    {
        private VoiceService _voice;
        public Form1()
        {
            InitializeComponent();
        }
        private bool _voskInited = false;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_voskInited) return;
            _voskInited = true;

            try
            {
                Environment.SetEnvironmentVariable(
                    "PATH",
                    AppDomain.CurrentDomain.BaseDirectory + ";" +
                    Environment.GetEnvironmentVariable("PATH")
                );

                _voice = new VoiceService(Log);
                _voice.StartMQTT();
                _voice.StartVosk();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Vosk INIT FAILED");
            }
        }

        private void Log(string msg)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(Log), msg);
                return;
            }

            txtLog.AppendText(
                $"[{DateTime.Now:HH:mm:ss}] {msg}\r\n"
            );
        }
    }
}
