using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class SerialReader : MonoBehaviour
{
    public ControlUnit CU;
    [Header("Serial Settings")]
    public string portName = "";          // ����θ� �ڵ� Ž��
    public int baudRate = 115200;
    public bool autoOpenOnStart = true;
    public bool autoReconnect = true;
    public bool autoDetectPort = true;    // true�� �ڵ� Ž�� ���
    public int portOpenResetWaitMs = 500; // Uno �ڵ����� ���
    public int scanPerPortProbePackets = 3; // �� ��Ʈ���� �ִ� �� �� ��Ŷ �˻�����
    // --- Detect tuning (auto-detect phase only) ---
    private const int DETECT_WARMUP_MS = 1200; // ���� �� ����ȭ(��Ʈ�δ�+����ġ ����)
    private const int DETECT_FIND_START_MS = 1500; // START(0xAA) ã�� �� ����ѵ�
    private const int DETECT_READ_TIMEOUT_MS = 100;  // Ž�� �� per-read Ÿ�Ӿƿ�


    [Header("Raw ADC (0..1023)")]
    public ushort j1x;    // A0
    public ushort j1y;    // A1
    public ushort j2x;    // A2
    public ushort j2y;    // A3
    public ushort pot1;   // A4
    public ushort pot2;   // A5

    [Header("Buttons (pressed = true)")]
    public bool button1;  // bit0
    public bool button2;  // bit1
    public bool button3;  // bit2
    public bool button4;  // bit3
    public bool button5;  // bit4

    [Header("Stats")]
    public int packetsPerSecond;
    public int checksumErrors;
    public int framingErrors;
    public string connectedPort;          // ���� ����� ��Ʈ �̸�

    // --- 16B Packet spec ---
    // [0]  START = 0xAA
    // [1..12] 6 * uint16 (LE) A0..A5
    // [13] BTN (pressed=1 bits 0..4)
    // [14] CHK = sum(12 analog bytes + BTN) mod 256
    // [15] END   = 0x55
    private const byte START_BYTE = 0xAA;
    private const byte END_BYTE = 0x55;
    private const int PACKET_LEN = 16;
    private const int REM_AFTER_START = PACKET_LEN - 1; // 15
    private const int READ_TIMEOUT_MS = 20;
    private const int RECONNECT_WAIT_MS = 500;

    // --- Serial + Thread ---
    private SerialPort _sp;
    private Thread _rxThread;
    private volatile bool _run;

    private readonly object _lock = new object();
    private Parsed _latest;
    private int _packetsCounter;
    private float _ppsTimer;

    [Serializable]
    private struct Parsed
    {
        public ushort a0, a1, a2, a3, a4, a5;
        public byte btn;
        public bool valid;
    }

    void Start()
    {
        if (autoOpenOnStart) StartWorker();
    }

    void FixedUpdate()
    {
        Parsed snap;
        lock (_lock) snap = _latest;

        if (snap.valid)
        {
            j1x = snap.a0; j1y = snap.a1;
            j2x = snap.a2; j2y = snap.a3;
            pot1 = snap.a4; pot2 = snap.a5;

            button1 = (snap.btn & (1 << 0)) != 0;
            button2 = (snap.btn & (1 << 1)) != 0;
            button3 = (snap.btn & (1 << 2)) != 0;
            button4 = (snap.btn & (1 << 3)) != 0;
            button5 = (snap.btn & (1 << 4)) != 0;

            if(CU!= null)
            {
                CU.A0 = snap.a0; CU.A1 = snap.a1;
                CU.A2 = snap.a2; CU.A3 = snap.a3;
                CU.A4 = snap.a4; CU.A5 = snap.a5;

                CU.D2 = (snap.btn & (1 << 0)) != 0;
                CU.D3 = (snap.btn & (1 << 1)) != 0;
                CU.D4 = (snap.btn & (1 << 2)) != 0;
                CU.D5 = (snap.btn & (1 << 3)) != 0;
                CU.D6 = (snap.btn & (1 << 4)) != 0;
            }
        }

        _ppsTimer += Time.unscaledDeltaTime;
        if (_ppsTimer >= 1f)
        {
            packetsPerSecond = Interlocked.Exchange(ref _packetsCounter, 0);
            _ppsTimer = 0f;
        }
    }

    void OnDisable() => StopWorker();
    void OnDestroy() => StopWorker();

    // -------- Controls --------
    public void StartWorker()
    {
        StopWorker();
        _run = true;
        _rxThread = new Thread(RxLoop) { IsBackground = true, Name = "ArduinoSerialRx" };
        _rxThread.Start();
    }

    public void StopWorker()
    {
        _run = false;

        try { _sp?.Close(); } catch { }
        try { _sp?.Dispose(); } catch { }
        _sp = null;

        if (_rxThread != null)
        {
            try { _rxThread.Join(300); } catch { }
            _rxThread = null;
        }
        connectedPort = "";
    }

    // -------- RX Thread --------
    private void RxLoop()
    {
        while (_run)
        {
            try
            {
                EnsurePortOpenWithAutoDetect();

                // ���� ���� ����
                int b = SafeReadByte(_sp);
                if (b < 0) continue;
                if ((byte)b != START_BYTE) { framingErrors++; continue; }

                byte[] buf = new byte[REM_AFTER_START]; // 15B
                if (!ReadExact(_sp, buf, 0, buf.Length, READ_TIMEOUT_MS)) continue;

                if (buf[14] != END_BYTE) { framingErrors++; continue; }

                byte calcChk = 0;
                for (int i = 0; i < 12; i++) calcChk += buf[i];
                calcChk += buf[12];
                byte pktChk = buf[13];
                if (calcChk != pktChk) { checksumErrors++; continue; }

                ushort a0 = (ushort)(buf[0] | (buf[1] << 8));
                ushort a1 = (ushort)(buf[2] | (buf[3] << 8));
                ushort a2 = (ushort)(buf[4] | (buf[5] << 8));
                ushort a3 = (ushort)(buf[6] | (buf[7] << 8));
                ushort a4 = (ushort)(buf[8] | (buf[9] << 8));
                ushort a5 = (ushort)(buf[10] | (buf[11] << 8));
                byte btn = buf[12];

                var parsed = new Parsed { a0 = a0, a1 = a1, a2 = a2, a3 = a3, a4 = a4, a5 = a5, btn = btn, valid = true };
                lock (_lock) _latest = parsed;
                Interlocked.Increment(ref _packetsCounter);
            }
            catch (ThreadAbortException) { return; }
            catch (Exception)
            {
                // ��Ʈ ���� �� �ݰ� ��Ž��
                SafeClosePort();
                if (!autoReconnect) return;
                Thread.Sleep(RECONNECT_WAIT_MS);
            }
        }
    }

    // -------- Port Open w/ Auto Detect --------
    private void EnsurePortOpenWithAutoDetect()
    {
        if (_sp != null && _sp.IsOpen) return;

        if (!autoDetectPort && !string.IsNullOrEmpty(portName))
        {
            // ���� ��Ʈ ���
            OpenPort(portName);
            connectedPort = portName;
            return;
        }

        // �ڵ� Ž��
        string[] ports = SerialPort.GetPortNames();
        foreach (var p in ports)
        {
            if (!_run) return;

            if (TryOpenAndValidate(p))
            {
                connectedPort = p;
                return;
            }
            // �����ϸ� ���� ��Ʈ �õ�
            SafeClosePort();
        }

        // �ĺ� ���� �� ��� ��� �� ��õ�
        Thread.Sleep(RECONNECT_WAIT_MS);
        throw new Exception("No valid Arduino port found in auto-detect.");
    }

    private void OpenPort(string p)
    {
        _sp = new SerialPort(p, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = READ_TIMEOUT_MS,
            WriteTimeout = READ_TIMEOUT_MS,
            Handshake = Handshake.None,
            DtrEnable = true,   // Uno �ڵ����� Ʈ����
            RtsEnable = false
        };
        _sp.Open();

        // �ڵ� ���� ��� + ���� ����
        Thread.Sleep(portOpenResetWaitMs);
        try { _sp.DiscardInBuffer(); } catch { }
        try { _sp.DiscardOutBuffer(); } catch { }
    }

    private bool TryOpenAndValidate(string p)
    {
        try
        {
            OpenPort(p);

            // --- Ž�� ���� WARM-UP: ���� ���� �߰� ����ȭ �ð� ---
            // (OpenPort ���� ��� + ���� �߰� ��� = �� 1.5~2.0s ���� ����)
            Thread.Sleep(DETECT_WARMUP_MS);
            try { _sp.DiscardInBuffer(); } catch { }

            // --- Ž�� �߿��� Ÿ�Ӿƿ��� ũ�� ���� ---
            _sp.ReadTimeout = DETECT_READ_TIMEOUT_MS;

            int probes = Mathf.Max(1, scanPerPortProbePackets);
            var deadline = Environment.TickCount + DETECT_FIND_START_MS;

            for (int attempt = 0; attempt < probes; attempt++)
            {
                // START ����Ʈ�� ��ü deadline���� ���������� ��ĵ
                int b;
                while (_run && Environment.TickCount < deadline)
                {
                    b = SafeReadByte(_sp);
                    if (b == START_BYTE) // 0xAA �߰�
                    {
                        // ������ 15B ���� (Ž�� Ÿ�Ӿƿ� ���)
                        byte[] buf = new byte[REM_AFTER_START];
                        if (!ReadExact(_sp, buf, 0, buf.Length, DETECT_READ_TIMEOUT_MS))
                            break; // ���� attempt��

                        if (buf[14] != END_BYTE) break;

                        // üũ�� ���� (12 analog bytes + BTN)
                        byte calcChk = 0;
                        for (int i = 0; i < 12; i++) calcChk += buf[i];
                        calcChk += buf[12];
                        if (calcChk != buf[13]) break;

                        // ��ȿ ��Ŷ Ȯ��!
                        // � ������ �Ѿ �� ��� Ÿ�Ӿƿ����� ����
                        _sp.ReadTimeout = READ_TIMEOUT_MS;
                        return true;
                    }

                    // timeout���� -1 ���� �� ����: ���� ���
                }

                // attempt ���̿� ��¦ �� ����
                Thread.Sleep(10);
            }
        }
        catch
        {
            // �����ϰ� false
        }
        return false;
    }


    private void SafeClosePort()
    {
        try { _sp?.Close(); } catch { }
        try { _sp?.Dispose(); } catch { }
        _sp = null;
        connectedPort = "";
    }

    // -------- IO Utils --------
    private static int SafeReadByte(SerialPort sp)
    {
        try { return sp.ReadByte(); }
        catch (TimeoutException) { return -1; }
        catch { return -1; }
    }

    private static bool ReadExact(SerialPort sp, byte[] buffer, int offset, int count, int perReadTimeoutMs)
    {
        int got = 0;
        while (got < count)
        {
            try
            {
                sp.ReadTimeout = perReadTimeoutMs;
                int n = sp.Read(buffer, offset + got, count - got);
                if (n <= 0) return false;
                got += n;
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
        return true;
    }
}
