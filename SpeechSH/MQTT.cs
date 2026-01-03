using MQTTnet;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpeechSH
{
    public class MQTT
    {
        private IMqttClient mqttClient;
        private MqttClientOptions mqttOptions;

        string m_strBroker = "192.168.180.233";
        int m_iPort = 1883;
        string m_strTopic = "QP250424/Control/Relay/";
        string m_strSubcribe = "QP250424/Device/Relay/#";

        Action<string> Log;
        string[] m_strDeviceName = new string[]
        {
            "Đèn phòng khách",
            "Quạt trần",
            "Đèn phòng ngủ",
            "Điều hòa",
            "Tivi",
            "Đèn ban công"
        };
        public MQTT(Action<string> log)
        {
            Log = log;
        }
        public async Task ConnectMqtt()
        {
            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();

            mqttOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(m_strBroker, m_iPort)
                //.WithCredentials("plv_tpm", "1234")
                .WithClientId("ClientID_" + Guid.NewGuid().ToString("N"))
                .Build();

            mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                int relayIndex = int.Parse(topic.Replace(m_strSubcribe.Replace("#", ""), "")) - 1;

                await Task.CompletedTask;
            };

            mqttClient.ConnectedAsync += async e =>
            {
                Log("Đã kết nối MQTT thành công!");
                await mqttClient.SubscribeAsync(m_strSubcribe); //("QP250424/Device/Relay/#");
            };

            mqttClient.DisconnectedAsync += async e =>
            {
                Log("MQTT mất kết nối!! Kết nối lại sau 2s...");
                await Task.CompletedTask;
                mqttClient.Dispose();
                Thread.Sleep(2000);
                ConnectMqtt();
            };

            try
            {
                await mqttClient.ConnectAsync(mqttOptions);
            }
            catch (Exception ex)
            {
                Log("Connect failed: " + ex.Message);
            }
        }

        public void SendRelayCommand(int relayIndex, bool bOn)
        {
            if (!mqttClient.IsConnected)
            {
                Log($"Chưa kết nối MQTT!");
                return;
            }

            string topic = $"{m_strTopic}{relayIndex + 1}"; 
            string payload = bOn ? "ON" : "OFF";

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build();

            mqttClient.PublishAsync(message);
            string strTemp = payload == "ON" ? $"Bật thiết bị {m_strDeviceName[relayIndex]}" : $"Tắt thiết bị {m_strDeviceName[relayIndex]}";
            Log($"[SENT] {strTemp}");
        }
    }
}
