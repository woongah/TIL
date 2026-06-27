// 시스템 기본 핵심 프레임워크 패키지 참조
using System;
// 리스트 및 딕셔너리 같은 제네릭 컬렉션 인터페이스 참조
using System.Collections.Generic;
// 컴포넌트의 가동 속성 및 디자인 타임 동작 모델 정의 참조
using System.ComponentModel;
// ADO.NET 데이터 테이블 및 SQL 계열 통합 데이터 구조 제어 참조
using System.Data;
// 색상, 브러시, 포인트 구조체 등 2D 그래픽스 제어 참조
using System.Drawing;
// 하드디스크 파일 읽기, 쓰기 및 데이터 스트림 제어 참조
using System.IO;
// 아두이노 보드와 컴퓨터 간 시리얼(가상 COM) 포트 통신 참조
using System.IO.Ports;
// 배열 및 컬렉션 데이터의 지능형 필터링, 정렬, 쿼리 연산 참조
using System.Linq;
// TCP/IP 소켓 아키텍처 기반 네트워크 소통 모듈 참조
using System.Net.Sockets;
// 바이트 배열과 문자열 간 인코딩 문자셋 변환 제어 참조
using System.Text;
// 고속 JSON 직렬화 및 역직렬화 처리 전용 패키지 참조
using System.Text.Json;
// 멀티스레드 분산 제어 및 비동기 Task 처리 패키지 참조
using System.Threading.Tasks;
// 윈도우 그래픽 사용자 인터페이스(GUI) 위젯 폼 제어 참조
using System.Windows.Forms;
// EmguCV 영상 처리 머신비전 핵심 기동 라이브러리 참조
using Emgu.CV;
// EmguCV 비디오 이미지 필터 속성 상숫값 맵 참조
using Emgu.CV.CvEnum;
// 이미지 픽셀 구조 및 색상 매트릭스 컨테이너 참조
using Emgu.CV.Structure;
// 비전 알고리즘용 내부 원시 포인터 벡터 배열 제어 참조
using Emgu.CV.Util;
// SQLite 데이터베이스 연동용 EF Core ORM 패키지 참조
using Microsoft.EntityFrameworkCore;

// 스마트 팩토리 프로젝트의 고유 식별 주소 공간 정의
namespace SmartConveyorSystem
{
    // 메인 화면 UI 요소와 제어 알고리즘을 담는 부분 클래스 시작
    public partial class Form1 : Form
    {
        #region [전역 변수 및 데이터 모델 정의]

        // 아두이노 기기와 양방향 시리얼 통신을 수행하는 포트 객체 변수
        private System.IO.Ports.SerialPort _arduinoPort;
        // 미쓰비시 PLC 장비와 이더넷 소켓을 연결하는 네트워크 클라이언트
        private TcpClient _tcpClient;
        // 네트워크 스트림을 통해 실제 원시 바이너리가 이동하는 통로 변수
        private NetworkStream _netStream;
        // 현장에 고정된 미쓰비시 FX5U PLC의 타깃 IP 주소
        private readonly string _plcIp = "192.168.0.50";
        // 미쓰비시 SLMP 프로토콜에 할당된 표준 네트워크 포트 번호
        private readonly int _plcPort = 4523;
        // 멀티스레드 환경에서 PLC 패킷이 뒤섞이지 않게 차단하는 잠금 변수
        private readonly object _plcLock = new object();

        // 카메라 스트리밍 소스를 가로채어 가져오는 영상 캡처 변수
        private VideoCapture _capture;
        // 머신비전 이미지 분석 엔진이 작동 중인지 구별하는 상태 플래그
        private bool _isVisionRunning = false;
        // 이미지 프레임 버퍼 행렬 겹침을 방지하는 상호 배제 잠금 객체
        private readonly object _frameLock = new object();
        // 하나의 프레임 사진을 인공지능이 분석 중인지 체크하는 플래그 변수
        private bool _isProcessingFrame = false;

        // 컨베이어 제품 중 빨간색 결함이 발견되었는지 체크하는 세이프티 변수
        private bool _isDefectDetected = false;
        // 장비 데이터를 백그라운드에서 실시간 연속 수집하는 무한 루프 변수
        private bool _isPolling = false;
        // 실시간 서보모터 구동 변위를 모니터링하기 위한 50칸짜리 데이터 배열
        private double[] _chartDataX = new double[50];

        // 생산 이력 일지를 로컬 DB 파일에 밀어 넣을 행 데이터 모델 정의
        public class ProductionLog
        {
            // 데이터베이스 테이블의 고유 자동 증가 식별 일련번호
            public int Id { get; set; }
            // 시스템 사건 사고 및 정상 동작이 일어난 날짜와 시간
            public DateTime Timestamp { get; set; }
            // 시스템 로그 성격 등급 정보 필드 (INFO, WARN, ERROR)
            public string LogType { get; set; }
            // 현장 장비 구동 내역 및 고장 상세 상황 메시지 본문
            public string Message { get; set; }
        }

        // 외부 JSON 파일에 저장하고 연동할 로봇축 가동 명세서 레시피 구조 정의
        public class DeviceRecipe
        {
            // 모터 모듈 X축 목표 기동 정수 각도
            public int TargetX { get; set; }
            // 모터 모듈 Y축 목표 기동 정수 각도
            public int TargetY { get; set; }
            // 모터 모듈 Z축 목표 기동 정수 각도
            public int TargetZ { get; set; }
            // 현재 작업 중인 생산 공정 사양서의 명칭
            public string RecipeName { get; set; }
        }

        // 아두이노 통신 보드가 송신한 다중 센서 종합 JSON 패킷을 파싱할 해독 모델
        public class ArduinoSensorData
        {
            // 환경 감동 온습도 센서 측정 온도값 저장소
            public double temp { get; set; }
            // 환경 감동 온습도 센서 측정 습도값 저장소
            public double humi { get; set; }
            // 초음파 센서로 판별한 컨베이어 적재 물체 이격 거리 수치
            public double dist { get; set; }
            // 물리 기동 제어용 택트 스위치의 전력 온/오프 상태 부호
            public int sw { get; set; }
            // 설비 수평 보존 및 인터록 세이프티용 디지털 기울기 센서 신호
            public int tilt { get; set; }
            // 조도 센서 측정 작업장 조명 밝기 아날로그 스케일 수치
            public int light { get; set; }
            // 속도 가변 제어 장치 아날로그 변위 신호 수치
            public int pot { get; set; }
        }

        #endregion

        #region [초기화 및 데이터베이스 설정]

        // 클래스가 최초 기동 시 화면 메모리 생성과 동시에 호출되는 진입 생성자
        public Form1()
        {
            // 윈폼 도구 상자 폼 레이아웃 비주얼 요소 물리 생성 결합
            InitializeComponent();
            // 모니터링 스펙 선 차트의 타이틀, 레이블, 한계 영역 초기 설정
            InitializeChart();

            // 데이터베이스 창구를 한시적으로 연동 열기 수행
            using (var db = new AppDbContext())
            {
                // 지정 디렉토리에 SQLite db 물리 파일이 없으면 자동 신규 구축
                db.Database.EnsureCreated();
            }
        }

        // 하드디스크 내 SQLite 엔진 연동 인프라를 상속받아 연는 클래스 정의
        public class AppDbContext : DbContext
        {
            // 생산 정보 로그 행 데이터 구조군을 관리하는 컨테이너 테이블 매핑
            public DbSet<ProductionLog> ProductionLogs { get; set; }
            // DB 물리 연결 환경을 설정하기 위한 프레임워크 메서드 정의
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                // PC 운영체제의 윈도우 바탕화면 절대 경로 자동 역추적 획득
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                // 바탕화면 경로 하단에 저장할 DB 파일명을 조립 결합
                string dbPath = System.IO.Path.Combine(desktopPath, "smart_factory.db");
                // SQLite 전용 가동 구문 스트링 주소를 엔진으로 전달
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        // 차트 모듈에 실시간 배열을 밀어 넣고 스케일을 정형화하는 서식 세팅 메서드
        private void InitializeChart()
        {
            // ScottPlot 위젯 스크린에 실시간 버퍼 연동 주소 바인딩 등록
            formsPlot1.Plot.Add.Signal(_chartDataX);
            // 시각화 선 그래프창 중앙 최상단 메인 텍스트 명칭 표기
            formsPlot1.Plot.Title("X-Axis Motor Position Real-time Monitor");
            // 가로축 좌표가 시간 진행 단위를 의미함을 레이블 마킹
            formsPlot1.Plot.XLabel("Time (Ticks)");
            // 세로축 좌표가 모터 기동 변위 각도임을 레이블 마킹
            formsPlot1.Plot.YLabel("Position (Degree)");
            // 일반 표준형 서보모터 반경 한계치에 맞춰 Y 범위를 0~180도로 압착 고정
            formsPlot1.Plot.Axes.SetLimitsY(0, 180);
            // 적용 완료된 차트 레이아웃 스타일을 그래픽 스크린에 즉시 재투영
            formsPlot1.Refresh();
        }

        // 수집된 실시간 수치나 이상 경보 일지를 DB 파일과 로그창 위젯에 남기는 연산
        private void SaveLogToDb(string logType, string message)
        {
            // 하드디스크 결함으로 인한 파일 쓰기 에러 튕김 현상을 원천 방어
            try
            {
                // SQLite 통신 인프라 엔티티 개체 임시 오픈
                using (var db = new AppDbContext())
                {
                    // 단일 로그 열 사양에 맞춰 이력 인스턴스 조립 빌드
                    var log = new ProductionLog
                    {
                        // 현재 시점의 날짜와 상세 시/분/초 주입
                        Timestamp = DateTime.Now,
                        // 매개변수로 지정받은 오류 및 동작 성격 코드 기입
                        LogType = logType,
                        // 가동 로그 본문 텍스트 설명 구문 주입
                        Message = message
                    };
                    // 완성된 이력 개체를 데이터베이스 추가 리스트에 큐 대기
                    db.ProductionLogs.Add(log);
                    // 대기 상태의 행 메모리를 하드웨어 SQLite 물리 파일에 최종 저장
                    db.SaveChanges();
                }

                // 로그 출력 위젯이 화면상에 소멸하지 않고 완벽하게 실재하는지 판독
                if (txtLog.IsHandleCreated && !txtLog.IsDisposed)
                {
                    // 다른 작업스레드와의 동시 접근 충돌(크로스 스레드) 방지 대리자 실행
                    this.Invoke(new Action(() =>
                    {
                        // 로그 텍스트창 위젯 맨 밑줄 자리에 신규 일지 텍스트를 누적 출력
                        txtLog.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logType}] {message}\r\n");
                    }));
                }
            }
            // 예외 발생 시 시스템 크래시를 차단하고 정보만 디버그 창으로 전달
            catch (Exception ex)
            {
                // 비주얼 스튜디오 최하단 간이 출력 모니터창에 에러 본문 인쇄
                System.Diagnostics.Debug.WriteLine($"DB 저장 오류: {ex.Message}");
            }
        }

        #endregion

        #region [SLMP 프로토콜 패킷 작성 알고리즘]

        // PLC 데이터 메모리 대역 내용을 한 번에 패키지로 긁어오기 위한 원시 프레임 패킷 생성기
        private byte[] BuildSlmpReadPacket(ushort startAddress, ushort count)
        {
            // 바이트 시퀀스를 유연하게 조립 병합하기 위한 동적 리스트 생성
            List<byte> packet = new List<byte>();
            // 미쓰비시 SLMP 통신 규격 고정형 요청 시작 헤더 부호(3E 프레임) 삽입
            packet.AddRange(new byte[] { 0x50, 0x00 });
            // 제어할 네트워크 주소 기입 (본체 직접 제어: 0x00)
            packet.Add(0x00);
            // 제어 타깃 장비 국번 식별 데이터 주입 (기본값: 0xFF)
            packet.Add(0xFF);
            // 하드웨어 CPU 자체 모듈 I/O 접근 바이너리 주입 (0x03FF 리틀엔디언 정렬)
            packet.AddRange(new byte[] { 0xFF, 0x03 });
            // 타깃 스테이션 멀티 국번 공간 패딩 주입 (0x00)
            packet.Add(0x00);
            // 헤더 이후부터 기입할 순수 데이터 본문 바이트 총합 길이 명시 (12바이트)
            packet.AddRange(new byte[] { 0x0C, 0x00 });
            // PLC 보드가 응답을 주지 않을 때 통신 차단을 결정할 시간 상숫값 (약 4초)
            packet.AddRange(new byte[] { 0x10, 0x00 });
            // 레지스터 값을 연속 일괄 읽기 지시용 명령어 번호 주입 (0x0401 반전 정렬)
            packet.AddRange(new byte[] { 0x01, 0x04 });
            // 워드 공간 크기 조작 사양 서브커맨드 번호 명시 (0x0000)
            packet.AddRange(new byte[] { 0x00, 0x00 });
            // 읽어오고자 하는 선두 디바이스 레지스터 주소 번호의 하위 바이트 주입
            packet.Add((byte)(startAddress & 0xFF));
            // 읽어오고자 하는 선두 디바이스 레지스터 주소 번호의 상위 바이트 주입
            packet.Add((byte)((startAddress >> 8) & 0xFF));
            // 확장 확장성을 위해 비워둔 예비 패딩 공간 바이트 기입
            packet.Add(0x00);
            // 조회 타깃 메모리가 미쓰비시 데이터 레지스터(D) 영역임을 규격 상수 기입
            packet.Add(0xA8);
            // 이번 한 번의 패킷으로 한 장에 읽어올 워드 수량의 하위 바이트 명시
            packet.Add((byte)(count & 0xFF));
            // 이번 한 번의 패킷으로 한 장에 읽어올 워드 수량의 상위 바이트 명시
            packet.Add((byte)((count >> 8) & 0xFF));
            // 최종 조립 리스트를 전송용 정적 바이트 배열로 빌드 변환 후 리턴
            return packet.ToArray();
        }

        // PLC 제어 레지스터 공간 번지에 원격 숫자를 쓰기 연사하기 위한 강제 전송용 패킷 생성기
        private byte[] BuildSlmpWritePacket(ushort address, ushort value)
        {
            // 쓰기용 원시 바이너리 조립 전용 바이트 리스트 인스턴스 생성
            List<byte> packet = new List<byte>();
            // 3E 요청 헤더 고정식 바이너리 신호 비트 주입
            packet.AddRange(new byte[] { 0x50, 0x00 });
            // 네트워크 공유 라우터 주소 코드 및 장비 식별 국번 주입
            packet.Add(0x00); packet.Add(0xFF);
            // 연동 CPU 자체 모듈 접근용 바이너리 2바이트 기입
            packet.AddRange(new byte[] { 0xFF, 0x03 });
            // 연결 기기 멀티 다중국 번호 공간 공백 바이트 기입
            packet.Add(0x00);
            // 데이터를 주입 기입할 때 쓰이는 쓰기 전용 본문 전체 크기 명시 (14바이트)
            packet.AddRange(new byte[] { 0x0E, 0x00 });
            // 장비 이상 지연 발생 감시 타이머 바이트 기입
            packet.AddRange(new byte[] { 0x10, 0x00 });
            // 레지스터 값을 연속 일괄 쓰기 지시용 명령어 번호 주입 (0x1401 반전 정렬)
            packet.AddRange(new byte[] { 0x01, 0x14 });
            // 워드 공간 일괄 기입 세부 서브커맨드 번호 기입
            packet.AddRange(new byte[] { 0x00, 0x00 });
            // 제어 목표 대상 레지스터 공간 일련번호의 하위 1바이트 쪼개기 기입
            packet.Add((byte)(address & 0xFF));
            // 제어 목표 대상 레지스터 공간 일련번호의 상위 2바이트 쪼개기 기입
            packet.Add((byte)((address >> 8) & 0xFF));
            // 주소 확장 규격용 고정 바이너리 0x00 삽입
            packet.Add(0x00);
            // 제어 메모리가 미쓰비시 데이터 레지스터(D레지스터) 부호임을 선언 명시
            packet.Add(0xA8);
            // 정확히 1개의 연속 워드 레지스터 공간에만 주입하겠다고 수량 기입 (1개)
            packet.AddRange(new byte[] { 0x01, 0x00 });
            // 레지스터에 최종 저장시킬 물리 제어 데이터 변수의 하위 바이트 주입
            packet.Add((byte)(value & 0xFF));
            // 레지스터에 최종 저장시킬 물리 제어 데이터 변수의 상위 바이트 주입
            packet.Add((byte)((value >> 8) & 0xFF));
            // 쓰기 연산용 원시 프레임 패킷 바이너리 배열 최종 출력 반환
            return packet.ToArray();
        }

        #endregion

        #region [네트워크 연결 및 해제]

        // 통신 접속 상태를 반전 가동 및 활성화하는 대시보드 버튼 이벤트 제어 창구
        private async void btnConnect_Click(object sender, EventArgs e)
        {
            // 만약 현재 PLC 서버 인프라 소켓 망이 완전히 끊겨 차단되어 있는 상태라면
            if (_tcpClient == null || !_tcpClient.Connected)
            {
                // 원격지 통신 지연 및 네트워크 먹통 에러로 인한 튕김 방지락 구동
                try
                {
                    // TCP 소켓 소통을 전담할 인프라 네트워크 연결 통로 인스턴스 할당
                    _tcpClient = new TcpClient();
                    // 비동기 대기 기법으로 PLC 하드웨어 보드의 IP 및 포트로 다이렉트 소켓 오픈 사격
                    await _tcpClient.ConnectAsync(_plcIp, _plcPort);
                    // 접속 통신 회선 물리 대역의 데이터 스트림 읽기/쓰기 권한 파이프라인 매칭
                    _netStream = _tcpClient.GetStream();

                    // 네트워크 트래픽 지연 시 최대 대기 판정 읽기 시간을 1초로 제한 설정
                    _netStream.ReadTimeout = 1000;
                    // 네트워크 패킷 밀어 넣기 시 최대 허용 대기 전송 시간을 1초로 제한 설정
                    _netStream.WriteTimeout = 1000;

                    // 프로그램 연결 상태 버튼 위젯 글자를 가동 중 상태인 연결해제 서식으로 교체
                    btnConnect.Text = "연결 해제";
                    // 소켓 망 개방 동기화의 확실한 성공 일지를 DB 영구 저장소에 등재
                    SaveLogToDb("INFO", "미쓰비시 FX5U PLC 이더넷 소켓 개방 및 SLMP 통신 성공");

                    // PLC 성공 여부와 무관하게 아두이노 포트 개방 예외 에러를 분리 격리하기 위한 트라이문
                    try
                    {
                        // 가상 시리얼 통신 인터페이스 인프라 객체 신규 생성
                        _arduinoPort = new System.IO.Ports.SerialPort();
                        // 컴퓨터 장치 관리자에 할당 설정된 아두이노 우노 하드웨어 포트 명칭 대입
                        _arduinoPort.PortName = "COM6";
                        // 아두이노 내장 펌웨어 통신 스케치 보드 레이트 속도 규격 9600 맞춤 설정
                        _arduinoPort.BaudRate = 9600;
                        // 아두이노 전용 시리얼 라인 전격 가동 개방
                        _arduinoPort.Open();
                        // 시리얼 채널 확보 보고 일지를 영구 DB에 등록
                        SaveLogToDb("INFO", "아두이노 통합 센서 네트워크 시리얼 포트 개방 성공");
                    }
                    // 아두이노 선이 분리되어 있거나 포트 점유 충돌 시 구동되는 안전지대
                    catch (Exception ex)
                    {
                        // 비주얼 스튜디오 내부 출력 디버그 모니터 창에만 에러 내역 출력
                        System.Diagnostics.Debug.WriteLine($"아두이노 연결 실패: {ex.Message}");
                    }

                    // 무한 반복 스케줄러 동기화 루프가 구동 가능하도록 작동 플래그 참(true) 활성화
                    _isPolling = true;
                    // 0.15초 주기로 모든 하드웨어 신호를 교환하는 고속 데이터 폴링 연산 구동
                    StartAsyncPolling();
                }
                // 원격지 공유기 선 단선이나 포트 닫힘으로 소켓 개방 실패 시 진입 구역
                catch (Exception ex)
                {
                    // 에러 현황 문구를 긴급 윈도우 공지 경고 창으로 표출하여 현장 조치 유도
                    MessageBox.Show($"FX5U PLC 접속 실패: {ex.Message}");
                }
            }
            // 이미 장비들과 하드웨어 망 연결이 확보되어 도는 상태에서 사용자가 다시 누른 모드라면
            else
            {
                // 모든 소켓 버퍼 및 가상 시리얼 리소스를 파괴 및 물리 안전 폐쇄 유도
                DisconnectPlc();
                // 프로그램 연결 조작 버튼 위젯 상태 명칭 글자를 초기 연결 서식으로 변경 복구
                btnConnect.Text = "연결";
                // 장비의 정상 철수 동작 일지를 영구 보관용 로컬 DB 데이터에 백업 마킹
                SaveLogToDb("INFO", "FX5U PLC 통신 소켓 해제 완료");
            }
        }

        // 동작 중인 통신 자원을 강제 완전 초기화 및 윈도우 커널에 물리 반환하는 자원 회수 메서드
        private void DisconnectPlc()
        {
            // 무한 폴링 루프 가동 중지 조건부 연산을 위해 기동 스케줄러 트리거 종료 지시
            _isPolling = false;
            // 동기화 수집 연산이 스트림을 건드리는 타이밍과 겹치지 않게 뮤텍스 동기화 잠금 선점
            lock (_plcLock)
            {
                // 네트워크 패킷 통로 스트림을 닫고 내부 기동 리소스를 완전히 비우기 파괴
                if (_netStream != null) { _netStream.Close(); _netStream.Dispose(); _netStream = null; }
                // 소켓 통신 모듈 인프라를 완전히 오프시키고 내부 점유 메모리를 클리어 파괴
                if (_tcpClient != null) { _tcpClient.Close(); _tcpClient.Dispose(); _tcpClient = null; }
            }
            // 전역 아두이노 통신 인터페이스 변수가 메모리상에 좀비 개체로 남았는지 검증
            if (_arduinoPort != null)
            {
                // 가상 컴포트 인터페이스 통로가 아직 열려 가동 중이라면 물리 셧다운 클로즈 지시
                if (_arduinoPort.IsOpen) _arduinoPort.Close();
                // 아두이노 개체가 물고 늘어지던 가상 시리얼 커널 하드웨어 리소스 완전 파괴
                _arduinoPort.Dispose();
                // 아두이노 전역 제어 변수 참조 위치를 빈 주소로 초기화
                _arduinoPort = null;
            }
        }

        #endregion

        #region [하드웨어 실시간 데이터 통합 동기화 루프]

        // 아두이노와 PLC 통신을 크로스 중계 동기화하며 화면과 차트를 초고속 갱신하는 비동기 메서드
        private async void StartAsyncPolling()
        {
            // 회선 장애 단선 판정을 누적 가산할 스코어 카운터 변수
            int consecutiveErrors = 0;
            // 차트 라인의 롤링 효과 가동 전 처음 50칸을 평행선으로 펴기 위한 판별 변수
            bool isFirstRead = true;
            // PLC의 모니터링 주소인 D100 번지부터 워드 10개를 일괄 긁어올 원시 조회 명령 패킷
            byte[] readCmd = BuildSlmpReadPacket(100, 10);
            // PLC 하드웨어가 네트워크 응답으로 회신해 줄 바이너리 바이트 배열 보관 바구니
            byte[] responseBuffer = new byte[100];

            // 프로그램 연결을 정상 오프해제 시키기 전까지는 멈춤 없이 무한 기동
            while (_isPolling)
            {
                // PLC 인프라 소켓 망 연결이 확보되지 않았다면 즉시 휴식 후 다음 주기로 패스 (보호 절)
                if (_tcpClient == null || !_tcpClient.Connected || _netStream == null)
                {
                    await Task.Delay(150);
                    continue;
                }

                // 데이터 수집 도중 발생하는 트래픽 단선 등 예외로 인한 대시보드 튕김 완벽 방어
                try
                {
                    // 이번 수집 주기에서 획득한 최종 모터 구동값 변수 공간 생성
                    ushort currentD0Value = 0;
                    // 아두이노 센서 기기로부터 정상 파싱 통신을 완수했는지 식별할 보조 가드 변수
                    bool isArduinoDataRead = false;

                    // 아두이노 포트가 열려있고 읽을 바이트 버퍼가 쌓여있는지 확인
                    if (_arduinoPort != null && _arduinoPort.IsOpen && _arduinoPort.BytesToRead > 0)
                    {
                        // 별도 정의된 아두이노 데이터 전용 처리 메서드를 호출하여 역직렬화 및 복사 동기화 집행
                        isArduinoDataRead = TryProcessArduinoData(out currentD0Value);
                    }

                    // 아두이노가 유실되었거나 읽지 못한 타이밍이라면 PLC 레지스터 직접 역조회 백업 루틴 구동
                    if (!isArduinoDataRead)
                    {
                        // PLC 이더넷 소켓 버퍼 충돌을 막는 락 영역에서 안전하게 패킷을 읽어오기
                        currentD0Value = ReadPlcBackupData(readCmd, responseBuffer);
                    }

                    // 모니터링 라인 선 차트 프레임에 이번 주기 각도 데이터를 차트 버퍼에 공급
                    if (isFirstRead)
                    {
                        // 최초 가동 주기 타이밍이므로 50칸짜리 전체 차트 평면 좌표를 현재 각도 수치로 채우기
                        for (int i = 0; i < _chartDataX.Length; i++) _chartDataX[i] = currentD0Value;
                        // 초기 세팅 완수로 플래그 취소
                        isFirstRead = false;
                    }
                    else
                    {
                        // 차트 메모리 배열 전체 데이터를 왼쪽 인덱스로 밀어내며 과거 0번 좌표 정보 소멸
                        Array.Copy(_chartDataX, 1, _chartDataX, 0, _chartDataX.Length - 1);
                        // 밀어내기 처리로 비어있는 맨 우측 마지막 자리에 이번 타임 최신 수치 주입
                        _chartDataX[_chartDataX.Length - 1] = currentD0Value;
                    }

                    // 차트 그래픽 컴포넌트 드라이버 핸들이 가시 화면 평면상에 안전하게 실재하는지 검증
                    if (formsPlot1.IsHandleCreated && !formsPlot1.IsDisposed)
                    {
                        // 화면 제어 Invoke 대리자를 기동하여 그래프 스크린에 새로고침 반영
                        this.Invoke(new Action(() =>
                        {
                            formsPlot1.Plot.Axes.AutoScale();
                            formsPlot1.Refresh();
                        }));
                    }

                    // 정상적으로 주기를 통과했으므로 연속 통신 오류 점수 초기화 카운트 리셋
                    consecutiveErrors = 0;
                }
                // 회선 완전 절단이나 네트워크 소켓 강제 유실 장애 예외가 포착되었을 때
                catch (Exception)
                {
                    // 네트워크 불통 상태 누적 카운터 정수 점수를 1 가산 누적
                    consecutiveErrors++;
                    // 통신 혼선이 아니고 무려 10회 주기 연속으로 소켓 리드가 완벽히 실종 장애 처리된 상황이라면
                    if (consecutiveErrors >= 10)
                    {
                        // 비동기 스레드 동기화 제어 처리를 강제 안전 종료 차단 수행
                        HandlePollingFailure();
                        // 가동 중이던 통신 무한 while 제어 루프를 아예 강제로 부수고 탈출
                        break;
                    }
                }

                // 메인 프로세서 과부하 점유율 폭등 및 전송 병목을 차단하고자 제어 수집 주기를 0.15초 휴식 분산 지정
                await Task.Delay(150);
            }
        }

        // [분리된 메서드 1]: 아두이노 시리얼 버퍼에서 데이터를 한 줄 읽어 JSON 해독 및 동기화를 전담하는 로직
        private bool TryProcessArduinoData(out ushort potValue)
        {
            // 반환할 가변저항 초기 출력 아웃 정수 변수 초기화
            potValue = 0;

            try
            {
                // 아두이노가 송신한 새 라인 직전까지의 문자 데이터를 읽어내고 불필요 여백 제거
                string serialData = _arduinoPort.ReadLine().Trim();

                // 패킷의 앞뒤가 무결한 중괄호 형태를 지닌 순수 가독 표준 JSON 텍스트 양식인지 확인 (보호 절)
                if (string.IsNullOrEmpty(serialData) || !serialData.StartsWith("{") || !serialData.EndsWith("}"))
                {
                    return false;
                }

                // 문자 데이터 더미를 C# 아두이노 전용 센서 클래스 개체 필드 구조로 지능형 변환 역직렬화 가동
                var sensorData = JsonSerializer.Deserialize<ArduinoSensorData>(serialData);
                // 파싱 정렬이 무결하게 끝난 알맹이가 존재하지 않는다면 즉시 중단 (보호 절)
                if (sensorData == null)
                {
                    return false;
                }

                // 아웃 출력 변수에 아두이노 가변저항 수치 임포트 저장
                potValue = (ushort)sensorData.pot;

                // 대시보드 위젯에 값을 뿌려주는 전용 서브 UI 업데이트 메서드 호출
                UpdateSensorDashboardUi(sensorData);

                // 하드웨어 릴레이 중계: 아두이노 수집 센서 데이터 정수 변위들을 PLC 특정 데이터 레지스터 워드 공간으로 비동기 사격 전송
                _ = WritePlcWordAsync(100, (ushort)sensorData.pot);
                _ = WritePlcWordAsync(104, (ushort)sensorData.temp);
                _ = WritePlcWordAsync(105, (ushort)sensorData.humi);
                _ = WritePlcWordAsync(106, (ushort)sensorData.dist);

                // 안전 인터록 세이프티 가드: 아두이노 기울기 센서가 넘어가서 충격을 감지했고 기존 비상 정지가 없는 타이밍이라면
                if (sensorData.tilt == 1 && !_isDefectDetected)
                {
                    // 인터록 모드 플래그 true 잠금
                    _isDefectDetected = true;
                    // 아두이노 보드 측에 Emergency 약속 문자 'E' 사격하여 경보 부저 구동
                    _arduinoPort.Write("E");
                    // PLC D103 주소에 즉각 벨트 가동 차단 정수 브레이크 신호 '1' 강제 원격 기입 주입
                    WriteRegisterWithRetry(103, 1);
                    // 전체 대시보드 바탕 화면 그래픽 컬러를 새빨간 Maroon 색상으로 즉시 강제 변경 호출
                    this.Invoke(new Action(() => { this.BackColor = Color.Maroon; }));
                    // 물리 인터록 비상 셧다운 상황 발생 상황 일지를 영구 DB 로그에 즉각 등록 기록
                    SaveLogToDb("WARN", "하드웨어 인터록: 설비 이상 기울어짐 감지! 비상 정지");
                }

                // 성공 판정 true 반환
                return true;
            }
            // 단순 읽기 타임아웃 예외 건너뛰기
            catch (TimeoutException) { return false; }
            // 에러 내역 문구를 비주얼 스튜디오 하단 출력 디버그 모니터 창 레이어로 전달
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"아두이노 파싱 에러: {ex.Message}");
                return false;
            }
        }

        // [분리된 메서드 2]: UI 스레드 크로스 크래시를 차단하며 안전하게 센서값을 위젯에 바인딩 매핑하는 로직
        private void UpdateSensorDashboardUi(ArduinoSensorData data)
        {
            // 현재 프로그램 윈도우 대시보드 UI 스레드가 파괴되지 않고 정상적으로 실재하는지 안전성 확인 (보호 절)
            if (!this.IsHandleCreated || this.IsDisposed)
            {
                return;
            }

            // 크로스 스레드 소멸 방지용 비동기 UI 연동 대리자 레이어 비긴인보크 실행
            this.BeginInvoke(new Action(() =>
            {
                // 실시간 온도 필드 텍스트를 소수점 첫째 자리 레이아웃 서식으로 레이블 UI 연동
                if (lblTemperature != null) lblTemperature.Text = $"온도: {data.temp:F1} °C";
                // 실시간 습도 필드 정밀 수치를 % 단위 글자와 함께 습도 레이블 UI 연동
                if (lblHumidity != null) lblHumidity.Text = $"습도: {data.humi:F1} %";
                // 초음파 사물 거리 측정 실시간 통과 수치를 거리 레이블 UI 문자열 연동
                if (lblDistance != null) lblDistance.Text = $"물체거리: {data.dist:F1} cm";
                // 광센서 밝기 등급 수치 연산 결과를 조도 수치 화면 표시 레이블 UI 연동
                if (lblLight != null) lblLight.Text = $"조도 수치: {data.light} lx";
                // 스위치 트리거 0, 1 변위 조건에 맞추어 컨베이어 물리 가동 메시지 선별 판정 출력
                if (lblSwitchStatus != null) lblSwitchStatus.Text = data.sw == 1 ? "스위치 ON (가동)" : "스위치 OFF";
            }));
        }

        // [분리된 메서드 3]: 아두이노 미기동 시 PLC 데이터 레지스터 직접 조회 백업 통신을 전담하는 로직
        private ushort ReadPlcBackupData(byte[] readCmd, byte[] responseBuffer)
        {
            // 다른 스레드의 소켓 스트림 동시 오염 및 간섭을 차단하기 위한 통신 락 뮤텍스 선점
            lock (_plcLock)
            {
                // PLC 측 이더넷 수신 버퍼 메모리로 D100 읽기 프로토콜 바이너리 프레임 사격 전송
                _netStream.Write(readCmd, 0, readCmd.Length);
                // PLC 보드가 되돌려 보낸 수신 바이너리를 패킷 바구니 배열에 긁어오고 실제 도달 바이트 수 반환
                int bytesRead = _netStream.Read(responseBuffer, 0, responseBuffer.Length);
                // 바이트 패킷 도달 규격이 맞고 미쓰비시 표준 응답 성공 사인이 완벽히 확보되었는지 정밀 확인
                if (bytesRead >= 11 && responseBuffer[0] == 0xD0 && responseBuffer[1] == 0x00)
                {
                    // PLC 내장 메인 처리 CPU의 연산 에러 상황 코드 상태 분석 종료 부호 연산
                    ushort endCode = (ushort)(responseBuffer[9] | (responseBuffer[10] << 8));
                    // 종료 에러 정수가 깔끔한 '0'값으로 통신의 완벽한 무결 응답 상태를 최종 승인했다면 데이터 추출
                    if (endCode == 0)
                    {
                        return (ushort)(responseBuffer[11] | (responseBuffer[12] << 8));
                    }
                }
            }
            // 통신 유실 등으로 조회가 정상 수행되지 못했을 때 반환할 예비 기본 각도 값 0 리턴
            return 0;
        }

        // [분리된 메서드 4]: 10회 연속 네트워크 단선 등 치명상 유실 발생 시 시스템 통신망 안전 격리 셧다운을 집행하는 로직
        private void HandlePollingFailure()
        {
            // 무한 가동 주기 트리거 플래그 변수 오프 강제 다운
            _isPolling = false;

            // UI 먹통 방지를 위해 크로스 스레드 회피 호출문을 개방하여 대시보드 오프라인 안전 모드 격리 진입
            this.Invoke(new Action(() => {
                // 공정 자동화 통신 제어 인프라망의 최종 파국 실종 사태 보고 일지를 DB에 박제 에러 등재
                SaveLogToDb("ERROR", "네트워크 통신 연속 유실 발생. 제어 차단.");
                // 생존 상태 여부가 의심되는 모든 원격 장치 하드웨어 소켓 연결망 통로를 폐쇄 격리 파괴
                DisconnectPlc();
                // 대시보드 커넥트 작동 단추 위젯 텍스트 서식을 초기 상태인 연결 모드로 강제 원상 복구
                btnConnect.Text = "연결";
            }));
        }

        // 별도의 워커 비동기 타스크 스레드 공간에서 PLC 특정 메모리로 1개 워드 쓰기 프로토콜 전송을 집행하는 로직
        private Task<bool> WritePlcWordAsync(ushort address, ushort value)
        {
            // 데이터 송신 동작 중 윈도우 대시보드 마우스 클릭 반응이 먹통이 되는 렉(Freezing) 현상을 물리 격리 차단
            return Task.Run(() =>
            {
                // 통신 소켓 버퍼 라인의 소유권을 배타적으로 확보하여 패킷 역전 충돌을 막는 잠금 제어 구동
                lock (_plcLock)
                {
                    // 네트워크 활성화 및 물리 커넥션 규격이 거짓 상태라면 실패 부호(false)를 던지며 함수 연산 즉시 조기 탈출
                    if (_tcpClient == null || !_tcpClient.Connected || _netStream != null) return false;
                    // 전송 도중 물리 회선 뽑힘 현상으로 인한 스트림 에러 다운 현상 격리용 트라이 가동
                    try
                    {
                        // 넘겨받은 디바이스 번지와 정수 제어값을 결합하여 미쓰비시 전용 쓰기 원시 패킷 바이너리 상자 팩 조립
                        byte[] writePacket = BuildSlmpWritePacket(address, value);
                        // 원격지 PLC 장비 측 파이프라인 전송 버퍼에 원시 조립 바이너리 패킷 강제 인젝션 사격
                        _netStream.Write(writePacket, 0, writePacket.Length);
                        // PLC 장비가 패킷을 받아 정상 승인했다고 회신해 줄 확인 응답 프레임을 수용할 20칸짜리 바구니 바이트 배열 생성
                        byte[] response = new byte[20];
                        // 회신 스트림을 읽어내어 바구니에 밀어 넣고 도달한 데이터 바이트 크기를 정수 획득 보관
                        int readLen = _netStream.Read(response, 0, response.Length);
                        // 수신 바이트 총량이 규격에 들고 응답 무결 프레임 헤더 플래그 부호가 검출되었는지 확인
                        if (readLen >= 11 && response[0] == 0xD0 && response[1] == 0x00)
                        {
                            // 응답 바이트 패킷 내부에 숨겨진 PLC 하드웨어 연산 에러 유무 코드를 해독 조합
                            ushort endCode = (ushort)(response[9] | (response[10] << 8));
                            // 가동 에러 결과 정수가 완벽한 클리어 상태인 '0' 신호 부호이면 성공 참(true) 리턴, 에러가 박혔으면 거짓(false) 리턴
                            return endCode == 0;
                        }
                        // 데이터 규격 미달 도달 시 실패 리턴
                        return false;
                    }
                    // 쓰기 스트림 장애 발생 시 무조건 실패 판정 부호 리턴 처리 후 연산 구역 격리 보호 탈출
                    catch { return false; }
                }
            });
        }

        // 무선 패킷 유실 상황에 대비해 원격 주입 실패 시 최고속으로 총 2회 보충 연속 발송을 집행하는 전송 신뢰 마스터 메서드
        private async void WriteRegisterWithRetry(ushort address, ushort value)
        {
            // 원본 송신 유실 시 보충해 줄 최대 연속 백업 재발송 카운터 임계치를 2회로 고정 설정
            int maxRetries = 2;
            // 0회차 마스터 최초 발송 기동부터 실패 시 연속 2회 서브 보충 루프를 순회하는 제어문 구동
            for (int i = 0; i <= maxRetries; i++)
            {
                // 원격 비동기 쓰기 연산 함수를 기동하여 한 방에 정상 성공 사인을 도달 받았다면 지체 없이 전체 함수 즉시 전격 조기 회군
                if (await WritePlcWordAsync(address, value)) return;
                // 전송 일시 지연 시 물리 전송 신호 정리를 도모하기 위해 0.1초 동안 아주 잠시 대기 유도 휴식 지정
            }
            // 재시도 필살 보충 카드까지 전부 망 유실로 수포로 돌아간 최종 제어 참사 전송 실패 정황을 DB 에러 이력 일지에 박제 각인
            SaveLogToDb("ERROR", $"SLMP 제어 기록 유실: D{address}");
        }

        #endregion

        #region [머신비전 컴퓨터 비전 AI 연산]

        // 모니터 대시보드 상의 스마트 팩토리 인공지능 비전 카메라 모듈 가동 / 정지 제어 스위치 버튼 클릭 핸들러
        private void btnStartVision_Click(object sender, EventArgs e)
        {
            // 만약 현재 실시간 컴퓨터 비전 AI 카메라 이미지 검출 알고리즘 엔진이 꺼진 대기 상태라면
            if (!_isVisionRunning)
            {
                // 스마트 카메라 하드웨어 초기화 및 드라이버 통신 에러 튕김을 전격 차단 예방
                try
                {
                    // 스마트폰 컴퓨터 스마트 팩토리 IP 웹캠 앱 또는 비전 장비 카메라의 원격 비디오 스트리밍 HTTP/RTSP 주소 할당
                    string cameraUrl = "http://192.168.0.16:8080/video";
                    // 입력한 타깃 통신 주소 인터페이스를 머신비전 전용 비디오 캡처 구동 드라이버 소스 매트릭스에 링크 할당
                    _capture = new VideoCapture(cameraUrl);
                    // 카메라 렌즈 장치가 새로운 가동 픽셀 사진 매트릭스를 포착할 때마다 실행할 실시간 인공지능 픽셀 추적 분석 알고리즘 메서드 바인딩 연동
                    _capture.ImageGrabbed += ProcessFrame;
                    // 비디오 캡처 인터페이스 장치로부터 실시간 이미지 픽셀 스트림 데이터 획득 구동 시동
                    _capture.Start();
                    // 전역 상태 제어 플래그 변수에 머신비전 감시 시스템 엔진이 전력 구동 중임을 의미하는 true 박제 선언
                    _isVisionRunning = true;
                    // 대시보드 스마트 카메라 가동 버튼 레이블 UI 가시 문구 명칭을 비전정지 서식 테마로 변경
                    btnStartVision.Text = "비전 정지";
                }
                // 카메라 링크 접속 실패 시 화면에 안내 메시지 표출 구역
                catch (Exception ex) { MessageBox.Show($"카메라 링크 에러: {ex.Message}"); }
            }
            // 스마트 카메라 AI 감시 체계가 기동 중인 상태에서 현장 작업자가 다시 버튼을 눌러 정지를 지시한 상태 모드라면
            else { StopVision(); }
        }

        // 카메라가 사진을 찍을 때마다 백그라운드 스레드에서 실시간 호출되는 핵심 컴퓨터 비전 AI 분석 엔진
        private void ProcessFrame(object sender, EventArgs e)
        {
            // 이전 장의 사진을 아직 분석 연산 중이라면 현재 프레임을 즉시 버리고 조기 탈출 (보호 절)
            if (_isProcessingFrame)
            {
                return;
            }

            // 영상 프레임 행렬 메모리에 멀티스레드가 동시 침범해 메모리가 깨지는 현상을 뮤텍스로 차단
            lock (_frameLock)
            {
                // 비디오 캡처 하드웨어 제어 포인터 자원이 소멸해 공백 상태라면 즉시 조기 리턴 (보호 절)
                if (_capture == null)
                {
                    return;
                }

                // C++ 라이브러리 연동 특성상 내부 메모리 예외를 격리 차단하기 위한 마스터 트라이 가동
                try
                {
                    // 현재 프레임 행렬 분석 연산에 진입했음을 선언 마킹하여 중복 프레임 진입 차단
                    _isProcessingFrame = true;

                    // 카메라 장치의 내부 원시 메모리 가상 주소 포인터가 손상되었다면 무조건 리턴 (보호 절)
                    if (_capture.Ptr == IntPtr.Zero)
                    {
                        return;
                    }

                    // 카메라 이미지 센서가 포착해 낸 가공 없는 순수 날것의 원시 픽셀 행렬 그릇 공간 생성
                    using (Mat frame = new Mat())
                    {
                        // 행렬 픽셀 값을 유효하게 긁어오는 데 실패했거나 내용물이 텅 비었다면 조기 격리 탈출 (보호 절)
                        if (!_capture.Retrieve(frame) || frame.IsEmpty)
                        {
                            return;
                        }

                        // 픽셀 연산 가공 및 인터록 판정 처리를 전담 서브 메서드로 완전 위임 분리
                        ExecuteVisionAnalysisPipeline(frame);
                    }
                }
                // 비전 연산 도중 발생하는 순간적인 예외는 공정 연속성 유지를 위해 조용히 패스
                catch (Exception) { }
                // 다음 주기에 들어올 비디오 사진 프레임을 정상 접수할 수 있도록 연산 중 플래그 해제 리셋
                finally { _isProcessingFrame = false; }
            }
        }

        // [분리된 메서드 1]: 원본 프레임을 회전하고 순차 필터 파이프라인을 구동하는 마스터 연산 세션
        private void ExecuteVisionAnalysisPipeline(Mat rawFrame)
        {
            // 원본 영상 훼손 예방 및 변형 처리를 위해 동일 면적 스케일의 영상 매트릭스 복제본 생성
            using (Mat clonedFrame = rawFrame.Clone())
            {
                // 컨베이어 카메라 거치 방향 뒤집힘 보정을 위해 이미지 매트릭스를 시계 방향으로 90도 회전
                CvInvoke.Rotate(clonedFrame, clonedFrame, RotateFlags.Rotate90Clockwise);

                // 불량품 색상 조건만 흰색으로 남겨 활성화할 이진화 마스크 행렬 그릇 생성
                using (Mat mask = new Mat())
                {
                    // 복제 영상과 색상 한계치를 전달하여 빨간색 불량 픽셀 영역만 분리해 흑백 마스크 생성
                    GenerateRedColorMask(clonedFrame, mask);

                    // 흑백 이진화 마스크 평면에서 불량 제품 윤곽선 좌표 경계선들을 역추적 연산
                    AnalyzeContoursAndTriggerInterlock(clonedFrame, mask);
                }

                // 인공지능 사각 경고 박스 마킹이 완료된 최종 비디오 행렬을 윈도우 대시보드 화면에 연동 출력
                DisplayProcessedFrameToUi(clonedFrame);
            }
        }

        // [분리된 메서드 2]: 가시광선 BGR 이미지를 HSV 평면으로 바꾸고 빨간색 영역만 흰색(255)으로 가려내는 로직
        private void GenerateRedColorMask(Mat sourceFrame, Mat targetMask)
        {
            // 조명 반사 오차 광원을 배제하고 순수 색상 정보만 정밀 계산할 HSV 특화 공간 행렬 수립
            using (Mat hsvFrame = new Mat())
            {
                // 일반 BGR 배열 매트릭스를 채도 연산 특화 모델인 HSV 디지털 색 영역 평면으로 고속 변환
                CvInvoke.CvtColor(sourceFrame, hsvFrame, ColorConversion.Bgr2Hsv);

                // 불량 사물(빨간색 플라스틱 사물 등) 조건의 최저 마지노선 HSV 한계 경계 구역 범위 하한 상수
                MCvScalar lowerRed = new MCvScalar(0, 100, 100);
                // 불량 사물(빨간색 플라스틱 사물 등) 조건의 최대 리미트 HSV 구역 범위 상한 상수
                MCvScalar upperRed = new MCvScalar(10, 255, 255);

                // 하한/상한 필터 상수를 EmguCV 핵심 엔진이 파싱 가능한 구조의 스칼라 어레이 레이어로 캡슐화
                using (var lowerScalar = new ScalarArray(lowerRed))
                using (var upperScalar = new ScalarArray(upperRed))
                {
                    // HSV 이미지 매트릭스를 순회 스캔하며 빨간색 범위에 드는 픽셀만 흰색(255)으로 변환해 마스크에 기입
                    CvInvoke.InRange(hsvFrame, lowerScalar, upperScalar, targetMask);
                }
            }

            // 미세 형광등 불빛 번짐으로 마스크 위에 튄 잔가시 점 노이즈들을 깎아 지워버리는 침식 필터 가동
            CvInvoke.Erode(targetMask, targetMask, null, new Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);
            // 노이즈가 제거된 불량품 순수 덩어리 면적 구역을 본래의 가시 면적 크기로 원상 복원하는 팽창 필터 가동
            CvInvoke.Dilate(targetMask, targetMask, null, new Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);
        }

        // [분리된 메서드 3]: 추출된 마스크를 바탕으로 면적 외곽선을 분석하고 규격 초과 시 비상 인터록을 발동하는 로직
        private void AnalyzeContoursAndTriggerInterlock(Mat visualFrame, Mat binaryMask)
        {
            // 흑백 마스크 매트릭스 평면에서 윤곽 경계 좌표들을 수용할 가상 메모리 벡터 공간 생성
            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {
                // 흑백 마스크 평면에서 양품 사물이 아닌 불량 독립 덩어리의 외곽 윤곽선 좌표 집합들을 정밀 추출
                CvInvoke.FindContours(binaryMask, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

                // 이번 분석 프레임 화면 상에서 최종 격리 대상 불량 결함 제품 포착 성공 여부를 판정할 변수
                bool redDetected = false;
                // 이미지 평면 전역에서 검출 완료된 전체 독립 불량 사물 윤곽 기하학 개수 카운트 획득
                int contourCount = contours.Size;

                // 포착 검출 완료된 개별 윤곽 기하 사물 개수 크기만큼 회전 루프를 구동하며 면적 검증 돌입
                for (int i = 0; i < contourCount; i++)
                {
                    // i번째 포착된 독립 불량 기하 사물의 외곽 좌표 라인 요소 인스턴스 임시 연동
                    using (VectorOfPoint contour = contours[i])
                    {
                        // 단순 잔상 불빛 오차가 아닌 실재 제품 크기 사양(500픽셀 면적 이상)에 맞는지 판정
                        if (CvInvoke.ContourArea(contour) > 500)
                        {
                            // 진짜 실 물리 불량 대상 제품이 통과 중임을 최종 물리 승인 마킹
                            redDetected = true;
                            // 불량 사물 외곽 좌표 집합 선을 사방 레이아웃 한계치로 완벽하게 감싸 안는 사각형 박스 구역 도출
                            Rectangle rect = CvInvoke.BoundingRectangle(contour);
                            // 화면 위 불량품 구역 정확한 자리에 선 두께 2픽셀짜리 새빨간 조준 사각 테두리 박스 드로잉
                            CvInvoke.Rectangle(visualFrame, rect, new MCvScalar(0, 0, 255), 2);
                            // 조준 적색 사각형 바로 위 상단 좌표 여백 공간 지점에 영어 대문자 비상 경고 타이틀 텍스트 "DEFECT" 적색 문자 인쇄
                            CvInvoke.PutText(visualFrame, "DEFECT", new Point(rect.X, rect.Y - 10), FontFace.HersheyPlain, 0.6, new MCvScalar(0, 0, 255), 2);
                        }
                    }
                }

                // 비전 AI 판독 결과 결함 사물이 발견되었고 기 가동된 비상 정지 플래그가 없다면 비상 차단 인터록 가동
                if (redDetected && !_isDefectDetected)
                {
                    // 대시보드 메인 마스터 인터록 세이프티 제어 플래그 변수를 비상사태 고장 셧다운 모드 true 값으로 잠금
                    _isDefectDetected = true;
                    // 프로그램 메인 대시보드 폼의 배경 컬러 테마를 비상 분위기 연출용 진한 적갈색(Maroon)으로 변경 호출
                    this.Invoke(new Action(() => { this.BackColor = Color.Maroon; }));
                    // PLC 원격 비상 브레이크 정지 명령 레지스터 주소 번지인 D103 공간에 가동 차단 신호 정수 '1' 원격 주입
                    WriteRegisterWithRetry(103, 1);
                    // 아두이노 제어 하드웨어 보드로 비상 경보 발령 약속 커맨드 문자 패킷 'E'를 시리얼 전송
                    if (_arduinoPort != null && _arduinoPort.IsOpen) _arduinoPort.Write("E");
                }
            }
        }

        // [분리된 메서드 4]: 가공이 완료된 비전 이미지 데이터를 .NET 비트맵으로 캐스팅하여 픽처박스 위젯에 드로잉하는 로직
        private void DisplayProcessedFrameToUi(Mat finalMatrix)
        {
            // 화면 중앙의 실시간 감시 스크린 비디오 박스 위젯이 메모리 소멸 없이 정상 팝업 표출 중인지 검증 (보호 절)
            if (!picCamera.IsHandleCreated || picCamera.IsDisposed)
            {
                return;
            }

            // 대시보드 모니터 UI 픽처박스 위젯에 최종 호환 연동하여 출력할 .NET 표준 비트맵 이미지 개체 주소 초기화
            Bitmap bitmapToDisplay = null;

            // EmguCV 전용 원시 바이너리 이미지 매트릭스 행렬 구조 데이터를 윈도우 C# GDI+ 표준 그래픽 객체 포맷 양식으로 변환 래핑
            using (Image<Bgr, byte> finalImage = finalMatrix.ToImage<Bgr, byte>())
            {
                // .NET 표준 비트맵 픽셀 바이트 메모리 구조로 완벽 카피 복사 복제본 인스턴스 최종 생성 획득
                bitmapToDisplay = new Bitmap(finalImage.Bitmap);
            }

            // 영상 출력용 고속 그래픽 스레드 간 픽셀 버퍼 충돌 사태를 완벽 배제하기 위한 비동기 UI 제어 대리자 연동 실행
            picCamera.BeginInvoke(new Action(() =>
            {
                // 가비지 메모리 누수 현상을 완전 차단하고자 직전 주기에 픽처박스가 붙잡고 화면에 뿌리던 과거 이미지 자원을 완전 해제 파괴
                if (picCamera.Image != null)
                {
                    var old = picCamera.Image;
                    picCamera.Image = null;
                    old.Dispose();
                }
                // AI 결함 조준 테두리 및 DEFECT 글자가 합성 완료된 최신 비트맵 그림을 실시간 비전 모니터 화면에 최종 바인딩 출력
                picCamera.Image = bitmapToDisplay;
            }));
        }

        // 가동 중인 비전 실시간 캡처 엔진 및 연동 이벤트를 안전 차단하고 하드웨어 자원을 OS 커널에 청정 반환하는 폐쇄 격리 메서드
        private void StopVision()
        {
            // 카메라 하드웨어 비디오 스트림 전용 전역 포인터 인스턴스가 실재 가동 상태로 존재하는 상황인지 검증
            if (_capture != null)
            {
                // 비디오 렌즈 센서에 수신 신호가 걸릴 때마다 주기적으로 시동되던 인공지능 분석 가동 이벤트 통로 델리게이트 연결선을 완전 분리 해제
                _capture.ImageGrabbed -= ProcessFrame;
                // 비디오 캡처 동작을 정지시키고 카메라가 OS 커널 드라이버로부터 빌려 쓰던 하드웨어 전용 캡처 리소스를 전량 폐쇄 완전 파괴 처리 후 빈 공간 초기화
                _capture.Stop(); _capture.Dispose(); _capture = null;
            }
            // 대시보드 상태 관리용 제어 변수 공간에 스마트 카메라 비전 시스템이 동작 정지 대기 모드임을 명시 기입
            _isVisionRunning = false;
            // 대시보드 카메라 화면 제어용 온오프 버튼 UI 글자 명칭 텍스트 서식을 초기 구동 준비 상태 문구인 비전시작 모드로 복구 변경
            btnStartVision.Text = "비전 시작";
        }

        // 현장 엔지니어가 프로그램 제어창 우측 상단 'X' 종료 단추를 마우스 클릭해 종료 절차를 밟을 때 윈도우 OS 시스템 커널이 알아서 자동 가동하는 클래스 소멸자 메서드
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 원격지 기기 오작동 사고 방지를 목표로 가동 중인 모든 PLC/아두이노 소켓 네트워크 연결선을 전격 분리 파괴하고 비전 분석 엔진을 클로즈 폐쇄 조치한 뒤 표준 창 닫기 소멸 규정 이행
            DisconnectPlc(); StopVision(); base.OnFormClosing(e);
        }

        #endregion

        #region [생산 공정 파일 레시피 데이터 관리]

        // 모니터 화면상의 로봇 제어 축 슬라이더 눈금 위치 변동 각도 정수 값들을 텍스트 규격 JSON 사양서로 로컬 파일 보관하는 백업 조작 버튼 핸들러
        private void btnSaveRecipe_Click(object sender, EventArgs e)
        {
            // 관리자 쓰기 거부 권한 부족 폴더 이상 사태 에러로 인한 윈도우창 먹통 크래시 완전 방어
            try
            {
                // 현재 대시보드 슬라이더 슬롯 트랙바 위젯들 눈금에 고정된 마우스 조절 정수 각도 변위 수치들을 한곳으로 흡수 취합하여 레시피 데이터 행 인스턴스로 조립 가공
                var recipe = new DeviceRecipe { RecipeName = "Standard_Red_Product_Recipe", TargetX = trackX.Value, TargetY = trackY.Value, TargetZ = trackZ.Value };
                // 가공이 완료된 C# 레시피 규격 구조체 데이터를 줄바꿈 레이아웃 형태의 문법 규격 JSON 포맷 문장 텍스트로 고속 직렬화 가공한 뒤 실행 파일 경로 내 디스크 공간에 "recipe.json" 이름 문자열 파일로 쓰기 영구 보관 집행
                File.WriteAllText("recipe.json", JsonSerializer.Serialize(recipe, new JsonSerializerOptions { WriteIndented = true }));
                // 공정 품질 가동 사양서 표준 설정 세팅 파일 문서 출력 완수 일지를 시스템 데이터베이스 로그 기록에 정상 추가
                SaveLogToDb("INFO", "공정 레시피 파일 저장 완료");
            }
            // 파일 쓰기 예외 발생 시 원인 상세 내역 경고 팝업창 공지
            catch (Exception ex) { MessageBox.Show($"레시피 저장 실패: {ex.Message}"); }
        }

        // 하드디스크에 보관되어 잠자던 기존 JSON 환경 설정 파일을 판독 부활시켜 프로그램 대시보드 슬라이더 눈금과 PLC 모터 구동 메모리 공간에 전면 동기화 이식하는 다운로드 단추 핸들러
        private void btnLoadRecipe_Click(object sender, EventArgs e)
        {
            // 만약 현재 프로그램 가동 루트 폴더 디렉토리 내에 과거에 저장 보관해 둔 "recipe.json" 사양서 텍스트 설정 파일이 원천 유실되어 부재한 상황이라면 연산 실익이 없으므로 즉시 가동 철수 조기 리턴
            if (!File.Exists("recipe.json")) return;
            // 파일 내 텍스트 훼손 등으로 인한 JSON 해독 불능 포맷 오류 크래시 차단 격리 트라이문 구동
            try
            {
                // 하드디스크 recipe.json 파일 속의 전 지문을 문자열로 통째로 인풋 잃어와 역직렬화 해독 연산 가공을 집행한 뒤 C# 정형화 개체 속성 인스턴스 레이어로 완전 조립 부활 완수
                DeviceRecipe recipe = JsonSerializer.Deserialize<DeviceRecipe>(File.ReadAllText("recipe.json"));
                // 불러오기 해독 연산 처리된 레시피 설정 사양 알맹이가 공백 주소가 아니고 유효하게 메모리상에 정렬 완료되었는지 검증
                if (recipe != null)
                {
                    // 대시보드 윈도우 조작 패널 평면 위에 생성 배치된 로봇 모터축 매핑 조절용 트랙바 위젯 눈금 슬라이더 초점 위치들을 불러온 데이터 속성 정수 사양 값으로 완전 동기화 위치 변경
                    trackX.Value = recipe.TargetX; trackY.Value = recipe.TargetY; trackZ.Value = recipe.TargetZ;
                    // 사용자가 한눈에 현재 강제 동기화 셋업 수치를 실시간 리딩하도록 디스플레이 텍스트 레이블 위젯 글자 내용물 수치를 변경 출력
                    lblX.Text = $"X: {trackX.Value}"; lblY.Text = $"Y: {trackY.Value}"; lblZ.Text = $"Z: {trackZ.Value}";

                    // 현재 스마트 컨베이어 하드웨어 라인 가동 상태에 비전/기울기 세이프티 차단 안전 인터록이 풀려 정상 기동이 허용되는 시점 상황인지 검증
                    if (!_isDefectDetected)
                    {
                        // 사양서 설정 레시피 기준값대로 로봇 암 구동 목표 정수 X 각도 변위를 타깃 미쓰비시 PLC 데이터 레지스터 D0 메모리 번지에 직통 전송 기입
                        WriteRegisterWithRetry(0, (ushort)trackX.Value);
                        // 사양서 설정 레시피 기준값대로 로봇 암 구동 목표 정수 Y 각도 변위를 타깃 미쓰비시 PLC 데이터 레지스터 D1 메모리 번지에 직통 전송 기입
                        WriteRegisterWithRetry(1, (ushort)trackY.Value);
                        // 사양서 설정 레시피 기준값대로 로봇 암 구동 목표 정수 Z 각도 변위를 타깃 미쓰비시 PLC 데이터 레지스터 D2 메모리 번지에 직통 전송 기입
                        WriteRegisterWithRetry(2, (ushort)trackZ.Value);
                    }
                    // 레시피 데이터 원격 장비 다운로드 이식 가동 공정이 완수되었음을 생산 이력 DB 일지 로그 데이터에 등재 백업
                    SaveLogToDb("INFO", "공정 레시피 PLC 제어 주입 완료");
                }
            }
            // 파일 불러오기 연산 실패 장애 시 원인 공지 경고 팝업창 표출
            catch (Exception ex) { MessageBox.Show($"레시피 로드 실패: {ex.Message}"); }
        }

        // 저장 전용 로컬 데이터베이스 테이블 속에 은밀히 누적 보관된 시스템 이력 전체 로그 데이터를 엑셀 호환 표준 텍스트 문서인 CSV 보고서 양식 파일로 초고속 원터치 일괄 출력 내보내기 처리하는 마스터 버튼 핸들러
        private async void btnExportCsv_Click(object sender, EventArgs e)
        {
            // 내보내기 파일 출력 도중 마우스 다중 더블클릭 연타로 인한 중복 파일 쓰기 IO 충돌 대참사를 원천 배제하고자 데이터 추출 시작과 동시에 CSV 단추 UI 위젯 작동 상태를 잠시 사용 불가 비활성화 전환
            btnExportCsv.Enabled = false;
            // 가동 보고서가 인쇄된 정확한 컴퓨터 시점 타임 스탬프 일자 수치들을 정밀 결합하여 전 세계에 유일무이 무결한 확장자 파일명 문자열 합성 조합 명시
            string fileName = $"ProductionReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            // 데이터베이스 데이터 검색 쿼리 실패 및 하드웨어 디스크 입출력 거부 예외 상황을 완벽 무마 격리하기 위한 보호 트라이문 시동
            try
            {
                // 로컬 로드용 데이터베이스 컨텍스트에서 쿼리 읽기 처리해 낸 로그 행 구조체 더미들을 담아 보관할 임시 메모리 바구니 리스트 변수 선언
                List<ProductionLog> logs;
                // SQLite 로컬 DB 접근 통로 인프라를 잠시 열어 기존 보관 중이던 전 로그 데이터 테이블 알맹이를 소환하되 가동 시간의 역순(최신순) 정렬 규칙을 부여해 비동기 기법으로 리스트 바구니에 고속 로드 적재
                using (var db = new AppDbContext()) { logs = await db.ProductionLogs.OrderByDescending(l => l.Timestamp).ToListAsync(); }
                // 만일 로드 추출해 온 데이터베이스 이력 일지 행 개수가 제로(0) 건으로 데이터베이스 파일 속이 완벽히 비었다면 처리할 의미가 상실되므로 즉시 잠갔던 버튼만 풀고 연산 조기 리턴 철수
                if (logs.Count == 0) { btnExportCsv.Enabled = true; return; }

                // 수만 건 이상의 대용량 엑셀 데이터 파일 추출 변환 시 전체 제어 프로그램 모니터 화면 레이아웃이 통째로 렉에 걸려 하얗게 굳어버리는 UI 응답 없음 프리징 현상을 완전히 막고자 비동기 Task 백그라운드 워커 스레드로 연산 작업 영역 격리 분산 실행
                await Task.Run(() =>
                {
                    // 지정한 고유 리포트 명칭으로 하드디스크 신규 문서 파일을 개설하되 해외 PC용 엑셀 등에서 한글 텍스트 깨짐 오류 현상을 완벽 배제하는 UTF-8 인코딩 문자셋 처리용 텍스트 저장 파이프라인 스트림 단이 전격 개방
                    using (var writer = new StreamWriter(fileName, false, Encoding.UTF8))
                    {
                        // 엑셀에서 파일을 임포트해 열었을 때 상단 구분선 가독성 확보 조치를 달성하기 위해 CSV 문서 파일 첫 줄 라인에 열 분류 타이틀 이름 식별 헤더 기입 기쇄
                        writer.WriteLine("로그ID,발생시간,로그형태,상세내용");
                        // 바구니에 로드해 온 방대한 로그 리스트 더미 행 데이터를 하나씩 순차 추출하여 텍스트 포맷 기입으로 내보내는 루프 구동
                        foreach (var log in logs)
                        {
                            // 로그 본문 설명 문장 안에 엑셀 CSV 구분 기호 문법인 쉼표(,) 문자가 무단으로 섞여 들어있다면 데이터가 인접 셀 칸으로 무단 분할 이탈해 버리는 치명적 문서 레이아웃 깨짐 현상이 유발되므로 본문 앞뒤 주소에 큰따옴표("") 문구를 코팅 보강하여 기호 오차 예방 클렌징 가공 처리 집행
                            string messageClean = log.Message.Contains(",") ? $"\"{log.Message}\"" : log.Message;
                            // 로그 인덱스 고유 식별 번호, 년월일시분초 지정 서식 시간 문자, 등급 부호명, 정제 완료된 일지 문장을 쉼표 기호 결합 규칙을 가미해 데이터 파일 1줄 행 텍스트로 밀어내어 기록
                        }
                    }
                });
                // 공정 가동 폴더 실행 디렉토리 하단 내에 무결한 엑셀 연동 문서가 완전 빌드 되었음을 알리는 리포트 작성 완료 안내 안내창 표출
                MessageBox.Show("생산 이력 보고서 출력 성공");
            }
            // 디스크 공간 거부 및 파일 권한 이상 장애 감지 시 팝업 공지 구역
            catch (Exception ex) { MessageBox.Show($"CSV 출력 실패: {ex.Message}"); }
            // 파일 생성 성공이든 예외 실패 낙방이든 최종 연산 수순 단계에 봉착했다면 사용자가 다음 보고서를 또 자유롭게 출력 보충 제어할 수 있도록 앞선 비활성화 잠금 장치를 전격 해제 복구
            finally { btnExportCsv.Enabled = true; }
        }

        #endregion

        #region [대시보드 수동 조작 및 슬라이더 스크롤 제어]

        // 작업 엔지니어가 마우스 드래그 방식으로 모터 수동 위치 조절 X축 트랙바 바를 스크롤 할 때 가동되는 구역으로, 안전 인터록 락다운 상태가 아니라면 수치 문자를 갱신 출력하고 동시에 원격지 PLC D0 레지스터에 최신 마우스 정수 각도 값을 즉시 강제 원격 기입 다운로드 주입 처리
        private void trackX_Scroll(object sender, EventArgs e) { if (_isDefectDetected) return; lblX.Text = $"X: {trackX.Value}"; WriteRegisterWithRetry(0, (ushort)trackX.Value); }
        // 작업 엔지니어가 마우스 드래그 방식으로 모터 수동 위치 조절 Y축 트랙바 바를 스크롤 할 때 가동되는 구역으로, 안전 인터록 락다운 상태가 아니라면 수치 문자를 갱신 출력하고 동시에 원격지 PLC D1 레지스터에 최신 마우스 정수 각도 값을 즉시 강제 원격 기입 다운로드 주입 처리
        private void trackY_Scroll(object sender, EventArgs e) { if (_isDefectDetected) return; lblY.Text = $"Y: {trackY.Value}"; WriteRegisterWithRetry(1, (ushort)trackY.Value); }
        // 작업 엔지니어가 마우스 드래그 방식으로 모터 수동 위치 조절 Z축 트랙바 바를 스크롤 할 때 가동되는 구역으로, 안전 인터록 락다운 상태가 아니라면 수치 문자를 갱신 출력하고 동시에 원격지 PLC D2 레지스터에 최신 마우스 정수 각도 값을 즉시 강제 원격 기입 다운로드 주입 처리
        private void trackZ_Scroll(object sender, EventArgs e) { if (_isDefectDetected) return; lblZ.Text = $"Z: {trackZ.Value}"; WriteRegisterWithRetry(2, (ushort)trackZ.Value); }

        // 대시보드 화면상에 생성된 소프트웨어 긴급 공정 브레이크 단추를 눌렀을 때 집행되는 긴급 수순 이벤트로, 미쓰비시 PLC D103 메모리에 강제 셧다운 부호 정수 '1'을 지체 없이 전송 연사하고 윈도우 대시보드 화면 컬러를 빨갛게 차단 잠금
        private void btnEmergency_Click(object sender, EventArgs e) { WriteRegisterWithRetry(103, 1); this.BackColor = Color.Maroon; }

        // 공정 결함 폭탄 및 기기 이상 인터록이 트리거되어 정지 차단 마비된 라인을 엔지니어가 육안 안전 확인 수행 후 정상 초기 기동 체계로 원격 전격 해제 및 시동 복구하는 소프트웨어 리셋 마스터 핸들러
        private void btnReset_Click(object sender, EventArgs e)
        {
            // 이미지 분석 비전 스레드가 리셋 순간에 픽셀 연산 교차 충돌을 야기해 다운되지 않도록 메모리 가드 영역 잠금 뮤텍스 락 확보
            lock (_frameLock)
            {
                // 전역 사물 결함 검출 인터록 수용 플래그 변수를 양품 상시 기동 조건인 깨끗한 대기 모드 false 값으로 전면 초기화 해제
                _isDefectDetected = false;
                // 비상 정지 불량 덩어리 영역 박스 딱지가 박제 상태로 얼어붙어 있던 직전 주기의 카메라 캡처 스크린샷 이미지 리소스를 윈도우 메모리 공간에서 확실히 파괴 및 완전 비우기
                if (picCamera.Image != null) { picCamera.Image.Dispose(); picCamera.Image = null; }
                // 적갈색 경보 위험 칼라 테마로 삼엄하게 잠겨있던 메인 대시보드 프로그램 윈도우 창 폼 바탕 컬러를 OS 규격 기본 회색(Control) 시스템 기본 폼 컬러 배색으로 원상 복구
                WriteRegisterWithRetry(103, 0);
                // 통신 링크 포트망이 연결 확보된 아두이노 스케치 제어 보드로 복구 해제 커맨드 시그널 문자 'R'을 시리얼 전송하여 하드웨어 부저를 무조건 음소거시키고 시끄러운 적색 경고등 소등 원격 해제 지시
                if (_arduinoPort != null && _arduinoPort.IsOpen) _arduinoPort.Write("R");
                // 메인 제어망 인프라의 모든 공정 원격 하드웨어 네트워크 락 해제 공정이 무결하게 종료 완수되었음을 화면 안내창으로 전격 최종 알림 통보 공지
                MessageBox.Show("인터록 해제 및 PLC 제어 시스템 복구 완료.");
            }
        }

        #endregion
    }
}