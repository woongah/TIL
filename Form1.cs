using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
// Emgu CV namespace
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
// EF Core namespace
using Microsoft.EntityFrameworkCore;
using NModbus;
using NModbus.Device;
using NModbus.Serial;


namespace SmartConveyorSystem
{

    public partial class Form1 : Form
    {

        private SerialPort _serialPort;
        private IModbusSerialMaster _modbusMaster;
        private const byte SlaveId = 1; // Modbus slave ID

        // 머신비전 변수 선언
        private VideoCapture _capture;
        private bool _isVisionRunning = false;
        // 불량품 감지 플래그 (중복 신호 전송 방지)
        private bool _isDefectDetected = false;
        private readonly object _frameLock = new object(); // 클래스 상단 전역 변수 구역에 있는지 확인

        // 실시간 차트 및 비동기 폴링을 위한 전역 변수
        private bool _isPolling = false;
        private double[] _chartDataX = new double[50]; // 차트에 표시할 최근 50개 데이터 버퍼
        private int _dataCount = 0;



        public Form1()
        {
            InitializeComponent();
            LoadAvailablePorts();
            InitializeChart(); // 차트 초기화 메서드 호출

            using (var db = new AppDbContext())
            {
                db.Database.EnsureCreated();
            }
        }

        public class AppDbContext : DbContext
        {
            public DbSet<ProductionLog> ProductionLogs { get; set; }
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                // ⭕ [수정] 현재 로그인한 윈도우 사용자의 '바탕화면'에 DB 파일이 생성되도록 설정
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string dbPath = System.IO.Path.Combine(desktopPath, "smart_factory.db");

                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        private void LoadAvailablePorts()
        {
            cmbPort.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            cmbPort.Items.AddRange(ports);
            if(cmbPort.Items.Count > 0)
                cmbPort.SelectedIndex = 0; // Select the first available port by default
        }

        // ScottPlot 차트 기본 세팅
        private void InitializeChart()
        {
            // 빈 데이터로 초기 그래프 라인 생성 (X축 모터의 실시간 궤적을 그림)
            formsPlot1.Plot.Add.Signal(_chartDataX);
            formsPlot1.Plot.Title("X-Axis Motor Position Real-time Monitor");
            formsPlot1.Plot.XLabel("Time (Ticks)");
            formsPlot1.Plot.YLabel("Position (Degree)");
            formsPlot1.Plot.Axes.SetLimitsY(0, 180); // 모터 구동 범위 0~180도 고정
            formsPlot1.Refresh();
        }

        private void SaveLogToDb(string logType, string message)
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    var log = new ProductionLog
                    {
                        Timestamp = DateTime.Now,
                        LogType = logType,
                        Message = message
                    };
                    db.ProductionLogs.Add(log);
                    db.SaveChanges();
                }

                this.Invoke(new Action(() =>
                {
                    txtLog.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logType}] {message}\r\n");
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB 저장 오류: {ex.Message}");
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                try
                {
                    if (cmbPort.SelectedItem == null)
                    {
                        MessageBox.Show("COM 포트를 선택해주세요.");
                        return;
                    }

                    // 아두이노 Modbus Slave와 통신 규격 일치시키기 (9600, 8-N-1)
                    _serialPort = new SerialPort(cmbPort.SelectedItem.ToString(), 9600, Parity.None, 8, StopBits.One);
                    _serialPort.Open();

                    // Modbus RTU Master 생성
                    var factory = new ModbusFactory();
                    _modbusMaster = factory.CreateRtuMaster(new SerialPortAdapter(_serialPort));
                    _modbusMaster.Transport.ReadTimeout = 1000; // 1초 타임아웃
                    _modbusMaster.Transport.WriteTimeout = 1000;

                    btnConnect.Text = "연결 해제";
                    MessageBox.Show("아두이노(Modbus Slave)에 성공적으로 연결되었습니다.");
                    SaveLogToDb("INFO", "아두이노 제어기 통신 회선 연결 성공");

                    // 폴링 플래그를 켜고 비동기 루프 가동 시작!
                    _isPolling = true;
                    StartAsyncPolling();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"연결 실패: {ex.Message}");
                }
            }
            else
            {
                // 연결 해제 로직
                _isPolling = false;
                _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
                _modbusMaster = null;
                btnConnect.Text = "연결";
                SaveLogToDb("INFO", "아두이노 제어기 통신 회선 연결 해제");
            }
        }

        // async/await 기반 비동기 Modbus 데이터 수집 루프 (멀티스레딩 구현)
        private async void StartAsyncPolling()
        {
            int consecutiveErrors = 0; // 연속 오류 카운터

            while (_isPolling)
            {
                if (_modbusMaster != null && _serialPort != null && _serialPort.IsOpen)
                {
                    try
                    {
                        // UI 스레드를 멈추지 않고(Non-blocking) 백그라운드 스레드에서 아두이노의 레지스터 값을 읽어옴
                        // Task.Run을 통해 통신 연산을 별도 워커 스레드로 완전히 위임
                        ushort[] registers = await Task.Run(() =>
                            _modbusMaster.ReadHoldingRegisters(SlaveId, 0, 4)
                        );

                        consecutiveErrors = 0;

                        // 읽어온 데이터(registers[0] = 현재 X축 값)를 차트 배열에 밀어 넣기 (Queue 구조 모사)
                        Array.Copy(_chartDataX, 1, _chartDataX, 0, _chartDataX.Length - 1);
                        _chartDataX[_chartDataX.Length - 1] = registers[0];

                        // 크로스 스레드 예외를 방지하며 안전하게 UI 차트 컨트롤 갱신
                        this.Invoke(new Action(() =>
                        {
                            // [ScottPlot v5 대응] 축 범위를 현재 데이터에 맞게 자동 정렬 후 리프레시
                            formsPlot1.Plot.Axes.AutoScale();
                            formsPlot1.Refresh();
                        }));
                    }
                    catch (Exception ex)
                    {
                        consecutiveErrors++;
                        System.Diagnostics.Debug.WriteLine($"폴링 통신 오류 ({consecutiveErrors}회): {ex.Message}");

                        if (consecutiveErrors >= 5)
                        {
                            _isPolling = false;
                            this.Invoke(new Action(() => {
                                SaveLogToDb("ERROR", "아두이노 통신 연속 타임아웃 발생. 물리 회선 탈락 징후로 인한 폴링 강제 종료.");
                                btnConnect_Click(null, null);
                                MessageBox.Show("장비와의 통신이 두절되었습니다. 케이블을 확인하세요.");
                            }));
                            break;
                        }
                    }
                }

                // UI를 먹통으로 만들지 않고 100ms(0.1초) 동안 대기하며 주기적 폴링 수행
                await Task.Delay(100);
            }
        }

        private async Task<bool> WriteRegisterWithRetryAsync(ushort address, ushort value)
        {
            int maxRetries = 3;
            int baseDelayMs = 200;

            for (int retry = 0; retry <= maxRetries; retry++)
            {
                if (_modbusMaster == null || _serialPort == null || !_serialPort.IsOpen) return false;

                try
                {
                    await Task.Run(() => _modbusMaster.WriteSingleRegister(SlaveId, address, value));
                    return true;
                }
                catch (Exception ex)
                {
                    if (retry == maxRetries)
                    {
                        SaveLogToDb("ERROR", $"주소 {address}에 데이터 쓰기 최종 실패. Max Retry 초과. 오류: {ex.Message}");
                        return false;
                    }

                    int delay = baseDelayMs * (int)Math.Pow(2, retry);
                    SaveLogToDb("WARN", $"통신 지연으로 인한 재시도 실행 ({retry + 1}/{maxRetries}). {delay}ms 후 재연결 시도...");
                    await Task.Delay(delay);
                }
            }
            return false;
        }

        private async void WriteRegister(ushort address, ushort value)
        {
            await WriteRegisterWithRetryAsync(address, value);
        }

        // --- 머신비전 기능 구현 구역 ---
        private void btnStartVision_Click(object sender, EventArgs e)
        {
            if (!_isVisionRunning)
            {
                try
                {
                    // [필독] 핸드폰 IP Webcam 앱에 뜨는 주소(IPv4)와 포트를 아래 양식에 맞게 넣으세요.
                    // 예: "http://192.168.0.5:8080/video" 또는 웹캠 사용 시 0 입력
                    string cameraUrl = "http://192.168.0.15:8080/video";

                    // 비디오 스트리밍 캡처 객체 생성
                    _capture = new VideoCapture(cameraUrl);

                    // 프레임이 수신될 때마다 실행될 이벤트 핸들러 연결
                    _capture.ImageGrabbed += ProcessFrame;
                    _capture.Start();

                    _isVisionRunning = true;
                    btnStartVision.Text = "비전 정지";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"카메라 연결 실패: {ex.Message}");
                }
            }
            else
            {
                StopVision();
            }
        }



        // [고도화] 색상 검출 및 아두이노 인터록 연동 비전 파이프라인


        private void ProcessFrame(object sender, EventArgs e)
        {
            // 멀티스레드 자원 경합 및 AccessViolationException 방지 자물쇠
            lock (_frameLock)
            {
                if (_capture == null || _capture.Ptr == IntPtr.Zero) return;

                using (Mat frame = new Mat())
                {
                    _capture.Retrieve(frame);
                    if (frame.IsEmpty) return;

                    // 비관리 메모리 충돌을 막기 위해 안전한 복사본(Clone) 생성
                    using (Mat clonedFrame = frame.Clone())
                    {
                        using (Mat hsvFrame = new Mat())
                        using (Mat mask = new Mat())
                        {
                            // 1. BGR 이미지를 HSV 이미지로 변환
                            CvInvoke.CvtColor(clonedFrame, hsvFrame, ColorConversion.Bgr2Hsv);

                            // 2. 빨간색 영역 지정을 위한 HSV 임계값 설정
                            ScalarArray lowerRed = new ScalarArray(new MCvScalar(0, 100, 100));
                            ScalarArray upperRed = new ScalarArray(new MCvScalar(10, 255, 255));
                            CvInvoke.InRange(hsvFrame, lowerRed, upperRed, mask);

                            // 3. 노이즈 제거 (Opening 연산)
                            CvInvoke.Erode(mask, mask, null, new Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);
                            CvInvoke.Dilate(mask, mask, null, new Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);

                            // 4. 윤곽선(Contours) 검출
                            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
                            {
                                CvInvoke.FindContours(mask, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

                                bool redDetected = false;

                                for (int i = 0; i < contours.Size; i++)
                                {
                                    using (VectorOfPoint contour = contours[i])
                                    {
                                        double area = CvInvoke.ContourArea(contour);

                                        if (area > 500)
                                        {
                                            redDetected = true;
                                            Rectangle rect = CvInvoke.BoundingRectangle(contour);

                                            // 원본 프레임(clonedFrame)에 사각형 및 텍스트 시각화 (폰트명 HersheyPlain으로 수정)
                                            CvInvoke.Rectangle(clonedFrame, rect, new MCvScalar(0, 0, 255), 2);
                                            CvInvoke.PutText(clonedFrame, "DEFECT", new Point(rect.X, rect.Y - 10),
                                                Emgu.CV.CvEnum.FontFace.HersheyPlain, 0.6, new MCvScalar(0, 0, 255), 2);
                                        }
                                    }
                                }

                                // 5. 불량품 감지 시 아두이노 인터록 제어 (Modbus 통신)
                                if (redDetected && !_isDefectDetected)
                                {
                                    _isDefectDetected = true;

                                    this.Invoke(new Action(() => {
                                        System.Diagnostics.Debug.WriteLine("비전 시스템: 불량품 감지! 즉시 정지 신호를 전송합니다.");
                                    }));

                                    WriteRegister(3, 1);
                                }
                            }
                        }

                        // ✨ [CS0103 해결 포인트] 모든 그래픽 처리가 완료된 clonedFrame을 기반으로 3.x 스타일 비트맵 추출
                        using (Image<Bgr, byte> finalImage = clonedFrame.ToImage<Bgr, byte>())
                        {
                            Bitmap bitmapToDisplay = finalImage.Bitmap;

                            // 최종 결과 화면을 UI 스레드에서 안전하게 갱신
                            picCamera.Invoke(new Action(() =>
                            {
                                if (picCamera.Image != null) picCamera.Image.Dispose(); // 메모리 누수 방지
                                picCamera.Image = bitmapToDisplay;                     // 화면 갱신
                            }));
                        }
                    }
                }
            }
        }

        private void StopVision()
        {
            if (_capture != null)
            {
                _capture.ImageGrabbed -= ProcessFrame;
                _capture.Stop();
                _capture.Dispose();
                _capture = null;
            }
            _isVisionRunning = false;
            btnStartVision.Text = "비전 시작";
        }

        // 폼이 닫힐 때 카메라 및 통신 리소스 안전하게 해제
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _isPolling = false;
            StopVision();
            if (_serialPort != null && _serialPort.IsOpen) _serialPort.Close();
            base.OnFormClosing(e);
        }

        private void btnSaveRecipe_Click(object sender, EventArgs e)
        {
            try
            {
                var recipe = new DeviceRecipe
                {
                    RecipeName = "Standard_Red_Product_Recipe",
                    TargetX = trackX.Value,
                    TargetY = trackY.Value,
                    TargetZ = trackZ.Value
                };

                string jsonString = JsonSerializer.Serialize(recipe, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText("recipe.json", jsonString);

                SaveLogToDb("INFO", "현재 수동 운전 좌표 기준 공정 레시피 파일(recipe.json) 저장 완료");
                MessageBox.Show("현재 설정이 recipe.json 파일로 저장되었습니다.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"레시피 저장 실패: {ex.Message}");
            }
        }
        private void btnLoadRecipe_Click(object sender, EventArgs e)
        {
            if (!File.Exists("recipe.json"))
            {
                MessageBox.Show("저장된 레시피 파일(recipe.json)이 존재하지 않습니다.");
                return;
            }

            try
            {
                string jsonString = File.ReadAllText("recipe.json");
                DeviceRecipe recipe = JsonSerializer.Deserialize<DeviceRecipe>(jsonString);

                if (recipe != null)
                {
                    trackX.Value = recipe.TargetX;
                    trackY.Value = recipe.TargetY;
                    trackZ.Value = recipe.TargetZ;

                    lblX.Text = $"X: {trackX.Value}";
                    lblY.Text = $"Y: {trackY.Value}";
                    lblZ.Text = $"Z: {trackZ.Value}";

                    if (!_isDefectDetected)
                    {
                        WriteRegister(0, (ushort)trackX.Value);
                        WriteRegister(1, (ushort)trackY.Value);
                        WriteRegister(2, (ushort)trackZ.Value);
                    }

                    SaveLogToDb("INFO", $"외부 공정 레시피 [{recipe.RecipeName}] 일괄 로드 및 설비 동기화 완료");
                    MessageBox.Show($"[{recipe.RecipeName}] 레시피를 성공적으로 불러와 장비에 주입했습니다.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"레시피 로드 실패: {ex.Message}");
            }
        }

        // [핵심 고도화] SQLite DB 데이터를 비동기로 추출하여 엑셀 호환 CSV 보고서 파일로 출력
        private async void btnExportCsv_Click(object sender, EventArgs e)
        {
            // 대용량 DB 조회 및 파일 스트림 쓰기 동안 UI 멈춤을 방지하기 위해 비동기(async/await) 적용
            btnExportCsv.Enabled = false;
            string fileName = $"ProductionReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            try
            {
                List<ProductionLog> logs;
                using (var db = new AppDbContext())
                {
                    // 비동기 쿼리로 데이터베이스 전체 로그 수집
                    logs = await db.ProductionLogs.OrderByDescending(l => l.Timestamp).ToListAsync();
                }

                if (logs.Count == 0)
                {
                    MessageBox.Show("출력할 생산 데이터 이력이 존재하지 않습니다.");
                    btnExportCsv.Enabled = true;
                    return;
                }

                // 파일 I/O 작업 분리 실행
                await Task.Run(() =>
                {
                    // 한글 인코딩 깨짐을 방지하기 위해 UTF-8 BOM 인코딩 세팅
                    using (var writer = new StreamWriter(fileName, false, Encoding.UTF8))
                    {
                        // 1. CSV 헤더(컬럼명) 작성
                        writer.WriteLine("로그ID,발생시간,로그형태,상세내용");

                        // 2. 데이터 레코드 순차 기록
                        foreach (var log in logs)
                        {
                            // 상세내용 문자열 내부에 혹시 모를 쉼표(,)가 포함되었을 경우를 대비한 캡슐화 처리
                            string messageClean = log.Message.Contains(",") ? $"\"{log.Message}\"" : log.Message;
                            writer.WriteLine($"{log.Id},{log.Timestamp:yyyy-MM-day HH:mm:ss},{log.LogType},{messageClean}");
                        }
                    }
                });

                SaveLogToDb("INFO", $"시스템 가동 이력 보고서 출력 성공 ({fileName})");
                MessageBox.Show($"생산 이력 보고서가 파일로 저장되었습니다.\n파일명: {fileName}", "보고서 출력 완료");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CSV 내보내기 실패: {ex.Message}");
            }
            finally
            {
                btnExportCsv.Enabled = true;
            }
        }


        // X축 스크롤바 조작 시 아두이노로 데이터 즉시 전송 (Holding Register 주소 0)
        private void trackX_Scroll(object sender, EventArgs e)
        {
            // ✨ 불량품이 검출된 상태(인터록)라면 명령을 전송하지 않고 즉시 리턴
            if (_isDefectDetected) return;
            lblX.Text = $"X: {trackX.Value}";
            WriteRegister(0, (ushort)trackX.Value);
        }
        // Y축 스크롤바 조작 시 아두이노로 데이터 즉시 전송 (Holding Register 주소 1)
        private void trackY_Scroll(object sender, EventArgs e)
        {
            // ✨ 불량품이 검출된 상태(인터록)라면 명령을 전송하지 않고 즉시 리턴
            if (_isDefectDetected) return;
            lblY.Text = $"Y: {trackY.Value}";
            WriteRegister(1, (ushort)trackY.Value);
        }
        // Z축 스크롤바 조작 시 아두이노로 데이터 즉시 전송 (Holding Register 주소 2)
        private void trackZ_Scroll(object sender, EventArgs e)
        {
            // ✨ 불량품이 검출된 상태(인터록)라면 명령을 전송하지 않고 즉시 리턴
            if (_isDefectDetected) return;
            lblZ.Text = $"Z: {trackZ.Value}";
            WriteRegister(2, (ushort)trackZ.Value);
        }
        // 비상 정지 버튼 클릭 시 (Holding Register 주소 3에 '1' 신호 전송)
        private void btnEmergency_Click(object sender, EventArgs e)
        {
            MessageBox.Show("비상 정지 신호를 전송합니다!");
            WriteRegister(3, 1); // 1을 보내 아두이노 측에서 동작을 멈추게 함
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            if (_modbusMaster == null || _serialPort == null || !_serialPort.IsOpen)
            {
                MessageBox.Show("아두이노가 연결되어 있지 않습니다. 통신을 먼저 확인하세요.");
                return;
            }

            // 1. C# 전역 차단 플래그 해제 (TrackBar 수동 제어 다시 허용)
            _isDefectDetected = false;

            // 2. 아두이노 Holding Register 3번 주소에 로우('0') 신호 전송하여 하드웨어 락 해제
            WriteRegister(3, 0);

            MessageBox.Show("인터록 해제 및 아두이노 제어 시스템 복구 완료. 정상 가동 상태로 복귀합니다.");
        }

        
        public class ProductionLog
        {
            public int Id { get; set; }
            public DateTime Timestamp { get; set; }
            public string LogType { get; set; }
            public string Message { get; set; }
        }

        public class DeviceRecipe
        {
            public int TargetX { get; set; }
            public int TargetY { get; set; }
            public int TargetZ { get; set; }
            public string RecipeName { get; set; }
        }
    } 

}
