using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArduinoControlApp
{
    public partial class Form1 : Form
    {
        //
        private SerialPort serialPort;

        public Form1()
        {
            InitializeComponent();
            serialPort = new SerialPort();


            // 아두이노가 데이터를 보냈을 때 자동으로 실행될 '이벤트 핸들러' 연결
            serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
        }

        // 화면이 켜질 때 현재 PC에 연결된 COM 포트 목록을 자동으로 가져옵니다.
        private void Form1_Load(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            cmbPort.Items.AddRange(ports);
            if (cmbPort.Items.Count > 0) cmbPort.SelectedIndex = 0; // 첫 번째 포트 기본 선택
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (cmbPort.SelectedItem == null)
            {
                MessageBox.Show("연결할 COM 포트를 선택해 주세요!", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!serialPort.IsOpen)
            {
                try
                {
                    serialPort.PortName = cmbPort.SelectedItem.ToString();
                    serialPort.BaudRate = 9600;
                    serialPort.Open();
                    MessageBox.Show($"{serialPort.PortName} 포트에 성공적으로 연결되었습니다.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"연결 실패: {ex.Message}");
                }
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
                MessageBox.Show("연결이 해제되었습니다.");
            }
        }

        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string inData = sp.ReadLine(); // 아두이노의 Serial.println() 데이터를 한 줄 읽어옴

            // 크로스 스레드 에러 방지를 위한 Invoke 처리 (UI 스레드에 데이터 전달)
            this.Invoke(new MethodInvoker(delegate {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] 수신: {inData}\r\n");
            }));
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            cmbPort.Items.Clear(); // 기존 목록 지우기
            string[] ports = SerialPort.GetPortNames(); // 현재 연결된 포트 새로 가져오기
            cmbPort.Items.AddRange(ports);

            if (cmbPort.Items.Count > 0) cmbPort.SelectedIndex = 0;
            else MessageBox.Show("연결된 아두이노를 찾을 수 없습니다.");
        }
    }
}
