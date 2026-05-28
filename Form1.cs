using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private ConveyorConfig _config = new ConveyorConfig();
        private logicSender _sender = new logicSender();

        public Form1()
        {
            InitializeComponent();
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            // [검증] 텍스트박스가 비어있는지 확인
            if (string.IsNullOrEmpty(tbxDirection.Text))
            {
                MessageBox.Show("방향을 입력해주세요.");
                return;
            }

            // [검증 및 형변환] NumericUpDown 값 int로 강제 변환
            int parsedWeight = (int)tbxWeight.Value;

            // [데이터 대입] 비밀 상자에 저장
            _config.Direction = tbxDirection.Text;
            _config.WeightLimit = parsedWeight;

            // [로직 호출] 통신 전문가에게 토스!
            _sender.SendToConveyor(_config);
        }

        public class ConveyorConfig
        {
            public string Direction { get; set; }

            public int WeightLimit { get; set; }
        }

        public class logicSender
        {
            public void SendToConveyor(ConveyorConfig config)
            {
                // 실제 통신 로직은 여기에 구현
                Console.WriteLine($"방향: {config.Direction}, 무게 제한: {config.WeightLimit}");
            }
        }
    }
}
