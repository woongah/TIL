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
// 이미지 처리(Emgu CV)에 필요한 네임스페이스들
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
// 로그를 로컬 DB에 저장하기 위해 가져온 Entity Framework Core
using Microsoft.EntityFrameworkCore;
using NModbus;
using NModbus.Device;
using NModbus.Serial;

namespace SmartConveyorSystem
{
    public partial class Form1 : Form
    {
        // 하드웨어(아두이노) 제어 및 Modbus RTU 통신을 위한 포트 변수들
        private SerialPort _serialPort;
        private IModbusSerialMaster _modbusMaster;
        private const byte SlaveId = 1; // Modbus slave ID
        private readonly object _modbusLock = new object(); // 통신 병목 방지용 자물쇠

        // 머신비전 변수 선언
        private VideoCapture _capture; // 카메라 스트리밍 영상을 받아오는 객체
        private bool _isVisionRunning = false; // 현재 비전 시스템이 켜져 있는지 체크하는 상태 스위치
        private readonly object _frameLock = new object(); // 이미지 연산 도중 다른 프레임이 치고 들어오지 못하게 묶어주는 자물쇠
        // 프레임 중복 처리 및 메모리 적체 방지 가드 플래그
        private bool _isProcessingFrame = false;

        // 불량품 감지 플래그 (중복 신호 전송 방지)
        private bool _isDefectDetected = false;
        
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
            public DbSet<ProductionLog> ProductionLogs { get; set; } // 가동 로그 테이블 매핑
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                // 현재 로그인한 윈도우 사용자의 '바탕화면'에 DB 파일이 생성되도록 설정
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string dbPath = System.IO.Path.Combine(desktopPath, "smart_factory.db");

                optionsBuilder.UseSqlite($"Data Source={dbPath}"); // 소형 프로젝트에 가벼운 SQLite를 적용
            }
        }

        private void LoadAvailablePorts()
        {
            cmbPort.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            cmbPort.Items.AddRange(ports);
            if (cmbPort.Items.Count > 0)
                cmbPort.SelectedIndex = 0; // 기본적으로 첫 번째 사용 가능한 포트 선택
        }

        // ScottPlot 차트 기본 세팅
        private void InitializeChart()
        {
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

                if (txtLog.IsHandleCreated && !txtLog.IsDisposed)
                {
                    this.Invoke(new Action(() =>
                    {
                        txtLog.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logType}] {message}\r\n");
                    }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB 저장 오류: {ex.Message}");
            }
        }

        private async void btnConnect_Click(object sender, EventArgs e)
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

                    _serialPort = new SerialPort(cmbPort.SelectedItem.ToString(), 9600, Parity.None, 8, StopBits.One);
                    _serialPort.Open();

                    var factory = new ModbusFactory();
                    _modbusMaster = factory.CreateRtuMaster(new SerialPortAdapter(_serialPort));

                    _modbusMaster.Transport.ReadTimeout = 200;
                    _modbusMaster.Transport.WriteTimeout = 200;

                    bool isDeviceValid = false;
                    try
                    {
                        lock (_modbusLock)
                        {
                            _modbusMaster.ReadHoldingRegisters(SlaveId, 0, 1);
                        }
                        isDeviceValid = true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[핸드셰이킹 거부 성공] 원인: {ex.Message}");
                        isDeviceValid = false;
                    }

                    if (!isDeviceValid)
                    {
                        if (_serialPort != null && _serialPort.IsOpen)
                        {
                            _serialPort.Close();
                            _serialPort.Dispose();
                        }
                        _serialPort = null;
                        _modbusMaster = null;

                        MessageBox.Show("선택한 포트에 장비(아두이노)가 응답하지 않습니다.\n포트 번호(COM)를 다시 확인하거나 케이블 연결을 체크하세요.",
                                        "통신 핸드셰이킹 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    btnConnect.Text = "연결 해제";
                    MessageBox.Show("아두이노(Modbus Slave)와 실시간 핸드셰이킹에 성공했습니다.");
                    SaveLogToDb("INFO", "아두이노 제어기 물리 검증 및 통신 회선 연결 성공");

                    _modbusMaster.Transport.ReadTimeout = 1000;
                    _modbusMaster.Transport.WriteTimeout = 1000;

                    _isPolling = true;
                    StartAsyncPolling();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"포트 개방 자체 실패 (권한 오동작): {ex.Message}");
                }
            }
            else
            {
                _isPolling = false;
                await Task.Delay(150);

                lock (_modbusLock)
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        _serialPort.Close();
                        _serialPort.Dispose();
                    }
                    _serialPort = null;
                    _modbusMaster = null;
                }

                btnConnect.Text = "연결";
                SaveLogToDb("INFO", "아두이노 제어기 통신 회선 안전하게 연결 해제");
            }
        }

        private async void StartAsyncPolling()
        {
            int consecutiveErrors = 0;

            while (_isPolling)
            {
                if (_modbusMaster != null && _serialPort != null && _serialPort.IsOpen)
                {
                    try
                    {
                        ushort[] registers = await Task.Run(() =>
                        {
                            lock (_modbusLock)
                            {
                                return _modbusMaster.ReadHoldingRegisters(SlaveId, 0, 4);
                            }
                        });

                        consecutiveErrors = 0;

                        Array.Copy(_chartDataX, 1, _chartDataX, 0, _chartDataX.Length - 1);
                        _chartDataX[_chartDataX.Length - 1] = registers[0];

                        if (formsPlot1.IsHandleCreated && !formsPlot1.IsDisposed)
                        {
                            this.Invoke(new Action(() =>
                            {
                                formsPlot1.Plot.Axes.AutoScale();
                                formsPlot1.Refresh();
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        consecutiveErrors++;
                        System.Diagnostics.Debug.WriteLine($"폴링 통신 오류 ({consecutiveErrors}회): {ex.Message}");

                        if (consecutiveErrors >= 5)
                        {
                            _isPolling = false;
                            this.Invoke(new Action(() => {
                                SaveLogToDb("ERROR", "아두이노 통신 연속 타임아웃 발생. 폴링 강제 종료.");
                                btnConnect_Click(null, null);
                                MessageBox.Show("장비와의 통신이 두절되었습니다. 케이블을 확인하세요.");
                            }));
                            break;
                        }
                    }
                }
                await Task.Delay(150);
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
                    await Task.Run(() =>
                    {
                        lock (_modbusLock)
                        {
                            _modbusMaster.WriteSingleRegister(SlaveId, address, value);
                        }
                    });
                    return true;
                }
                catch (Exception ex)
                {
                    if (retry == maxRetries)
                    {
                        SaveLogToDb("ERROR", $"주소 {address}에 데이터 쓰기 최종 실패. 오류: {ex.Message}");
                        return false;
                    }

                    int delay = baseDelayMs * (int)Math.Pow(2, retry);
                    SaveLogToDb("WARN", $"통신 지연으로 인한 재시도 실행 ({retry + 1}/{maxRetries}). {delay}ms 후 재연결 시도...");
                    await Task.Delay(delay);
                }
            }
            return false;
        }

        private void WriteRegister(ushort address, ushort value)
        {
            _ = WriteRegisterWithRetryAsync(address, value);
        }

        private void btnStartVision_Click(object sender, EventArgs e)
        {
            if (!_isVisionRunning)
            {
                try
                {
                    string cameraUrl = "http://192.168.0.15:8080/video";
                    _capture = new VideoCapture(cameraUrl);

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

        private void ProcessFrame(object sender, EventArgs e)
        {
            // 방어 1: 중복 연산으로 인한 네이티브 메모리 폭발 누적 차단
            if (_isProcessingFrame) return;

            lock (_frameLock)
            {
                if (_capture == null) return;

                try
                {
                    _isProcessingFrame = true;

                    if (_capture.Ptr == IntPtr.Zero) return;

                    using (Mat frame = new Mat())
                    {
                        try
                        {
                            if (!_capture.Retrieve(frame) || frame.IsEmpty) return;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[네이티브 프레임 추출 예외 패스]: {ex.Message}");
                            return;
                        }

                        // 안전한 복사본 처리 파이프라인
                        using (Mat clonedFrame = frame.Clone())
                        {
                            using (Mat hsvFrame = new Mat())
                            using (Mat mask = new Mat())
                            {
                                CvInvoke.CvtColor(clonedFrame, hsvFrame, ColorConversion.Bgr2Hsv);

                                MCvScalar lowerRed = new MCvScalar(0, 100, 100);
                                MCvScalar upperRed = new MCvScalar(10, 255, 255);

                                using (var lowerScalar = new ScalarArray(lowerRed))
                                using (var upperScalar = new ScalarArray(upperRed))
                                {
                                    CvInvoke.InRange(hsvFrame, lowerScalar, upperScalar, mask);
                                }

                                CvInvoke.Erode(mask, mask, null, new Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);
                                CvInvoke.Dilate(mask, mask, null, new Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);

                                using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
                                {
                                    CvInvoke.FindContours(mask, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

                                    bool redDetected = false;
                                    int contourCount = contours.Size;

                                    for (int i = 0; i < contourCount; i++)
                                    {
                                        using (VectorOfPoint contour = contours[i])
                                        {
                                            double area = CvInvoke.ContourArea(contour);

                                            if (area > 500)
                                            {
                                                redDetected = true;
                                                Rectangle rect = CvInvoke.BoundingRectangle(contour);

                                                CvInvoke.Rectangle(clonedFrame, rect, new MCvScalar(0, 0, 255), 2);
                                                CvInvoke.PutText(clonedFrame, "DEFECT", new Point(rect.X, rect.Y - 10),
                                                    FontFace.HersheyPlain, 0.6, new MCvScalar(0, 0, 255), 2);
                                            }
                                        }
                                    }

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

                            // 방어 2: 포인터를 완벽하게 끊어낸 독립 비트맵 픽셀 데이터 추출
                            Bitmap bitmapToDisplay = null;
                            using (Image<Bgr, byte> finalImage = clonedFrame.ToImage<Bgr, byte>())
                            {
                                bitmapToDisplay = new Bitmap(finalImage.Bitmap);
                            }

                            // UI 스레드 교차 업데이트 안전 검증 실행
                            if (picCamera.IsHandleCreated && !picCamera.IsDisposed)
                            {
                                picCamera.BeginInvoke(new Action(() =>
                                {
                                    if (picCamera.Image != null)
                                    {
                                        var oldImage = picCamera.Image;
                                        picCamera.Image = null;
                                        oldImage.Dispose(); // GDI+ 백그라운드 핸들 축적 즉각 파괴
                                    }
                                    picCamera.Image = bitmapToDisplay;
                                }));
                            }
                        }
                    }
                }
                catch (AccessViolationException ex)
                {
                    // 최후의 보루: C++ 레이어 메모리 위반 발생 시 프로그램을 죽이지 않고 건너뜀
                    System.Diagnostics.Debug.WriteLine($"[하드웨어 수준 보호 영역 침범 감지 및 우회 성공]: {ex.Message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[비전 프레임 일반 에러]: {ex.Message}");
                }
                finally
                {
                    _isProcessingFrame = false;
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
            catch (Exception ex) { MessageBox.Show($"레시피 저장 실패: {ex.Message}"); }
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
            catch (Exception ex) { MessageBox.Show($"레시피 로드 실패: {ex.Message}"); }
        }

        private async void btnExportCsv_Click(object sender, EventArgs e)
        {
            btnExportCsv.Enabled = false;
            string fileName = $"ProductionReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            try
            {
                List<ProductionLog> logs;
                using (var db = new AppDbContext())
                {
                    logs = await db.ProductionLogs.OrderByDescending(l => l.Timestamp).ToListAsync();
                }

                if (logs.Count == 0)
                {
                    MessageBox.Show("출력할 생산 데이터 이력이 존재하지 않습니다.");
                    btnExportCsv.Enabled = true;
                    return;
                }

                await Task.Run(() =>
                {
                    using (var writer = new StreamWriter(fileName, false, Encoding.UTF8))
                    {
                        writer.WriteLine("로그ID,발생시간,로그형태,상세내용");
                        foreach (var log in logs)
                        {
                            string messageClean = log.Message.Contains(",") ? $"\"{log.Message}\"" : log.Message;
                            writer.WriteLine($"{log.Id},{log.Timestamp:yyyy-MM-dd HH:mm:ss},{log.LogType},{messageClean}");
                        }
                    }
                });

                SaveLogToDb("INFO", $"시스템 가동 이력 보고서 출력 성공 ({fileName})");
                MessageBox.Show($"생산 이력 보고서가 파일로 저장되었습니다.\n파일명: {fileName}", "보고서 출력 완료");
            }
            catch (Exception ex) { MessageBox.Show($"CSV 내보내기 실패: {ex.Message}"); }
            finally { btnExportCsv.Enabled = true; }
        }

        private void trackX_Scroll(object sender, EventArgs e)
        {
            if (_isDefectDetected) return;
            lblX.Text = $"X: {trackX.Value}";
            WriteRegister(0, (ushort)trackX.Value);
        }

        private void trackY_Scroll(object sender, EventArgs e)
        {
            if (_isDefectDetected) return;
            lblY.Text = $"Y: {trackY.Value}";
            WriteRegister(1, (ushort)trackY.Value);
        }

        private void trackZ_Scroll(object sender, EventArgs e)
        {
            if (_isDefectDetected) return;
            lblZ.Text = $"Z: {trackZ.Value}";
            WriteRegister(2, (ushort)trackZ.Value);
        }

        private void btnEmergency_Click(object sender, EventArgs e)
        {
            MessageBox.Show("비상 정지 신호를 전송합니다!");
            WriteRegister(3, 1);
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            lock (_frameLock)
            {
                _isDefectDetected = false;
                if (picCamera.Image != null)
                {
                    picCamera.Image.Dispose();
                    picCamera.Image = null;
                }
                WriteRegister(3, 0);
                MessageBox.Show("인터록 해제 및 아두이노 제어 시스템 복구 완료. 정상 가동 상태로 복귀합니다.");
            }
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