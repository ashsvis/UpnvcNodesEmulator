using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

namespace UpnvcNodesEmulator
{
    public partial class EmulatorForm : Form
    {
        const int socketTimeOut = 3000;
        private readonly BackgroundWorker _worker;

        private static readonly ConcurrentDictionary<string, ushort> DictModbusItems =
            new ConcurrentDictionary<string, ushort>();

        private static MemIniFile mif = new MemIniFile("");

        public EmulatorForm()
        {
            InitializeComponent();

            mif.FromString(Properties.Resources.Descriptors);
            _worker = new BackgroundWorker {WorkerSupportsCancellation = true};
            _worker.DoWork += (o, args) =>
            {
                var worker = (BackgroundWorker)o;

                #region работа с TCP портом

                    var listener = new TcpListener(IPAddress.Any, 502)
                    {
                        Server = { SendTimeout = socketTimeOut, ReceiveTimeout = socketTimeOut }
                    };
                    Say = string.Format("Сокет TCP({0}) прослушивается...", 502);
                    do
                    {
                        Thread.Sleep(1);
                        try
                        {
                            listener.Start(10);
                            // Buffer for reading data
                            var bytes = new byte[256];

                            while (!listener.Pending())
                            {
                                Thread.Sleep(1);
                                if (!worker.CancellationPending) continue;
                                listener.Stop();
                                args.Cancel = true;
                                Say = string.Format("Сокет TCP({0}) - остановка прослушивания.", 502);
                                return;
                            }
                            var clientData = listener.AcceptTcpClient();
                            // создаем отдельный поток для каждого подключения клиента
                            ThreadPool.QueueUserWorkItem(arg =>
                            {
                                try
                                {
                                    // Get a stream object for reading and writing
                                    var stream = clientData.GetStream();
                                    int count;
                                    // Loop to receive all the data sent by the client.
                                    while ((count = stream.Read(bytes, 0, bytes.Length)) != 0)
                                    {
                                        Exchange(stream, bytes, count);
                                        Thread.Sleep(1);
                                    }
                                    // Shutdown and end connection
                                    clientData.Close();
                                }
                                catch (Exception ex)
                                {
                                    if (!worker.CancellationPending) Say = "Ошибка: " + ex.Message;
                                }
                            });
                        }
                        catch (SocketException exception)
                        {
                            if (!worker.CancellationPending)
                                Say = string.Format("Ошибка приёма: {0}", exception.Message);
                            break;
                        }
                    } while (!worker.CancellationPending);
                    listener.Stop();
                    Say = string.Format("Сокет TCP({0}) - остановка прослушивания.", 502);

                #endregion работа с TCP портом

            };
        }

        private void Exchange(NetworkStream stream, byte[] bytes, int count)
        {
            if (count < 8) return;
            var nodeAddr = bytes[6];
            var funcCode = bytes[7];
            using (var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                try
                {
                    var remoteEp = new IPEndPoint(IPAddress.Parse("10.9.3.55"), 502);
                    try
                    {
                        sock.Connect(remoteEp);
                        sock.SendTimeout = socketTimeOut;
                        var msgSend = new byte[count];
                        Array.Copy(bytes, msgSend, count);
                        // msg: [0][1][2][3][4][5] - заголовок: [4]*256+[5]=длина блока
                        // [6] - адрес устройства;
                        // [7] - код функции;
                        // [8][9] - начальный адрес регистров Modbus устройства;
                        // [10][11] - количество регистров.
                        var startAddr = Swap(BitConverter.ToUInt16(msgSend, 8));
                        sock.Send(msgSend);
                        var receivedBytes = new byte[1024];
                        sock.ReceiveTimeout = socketTimeOut;
                        var numBytes = sock.Receive(receivedBytes); //считали numBytes байт
                        sock.Disconnect(true);
                        // receivedBytes: [0][1][2][3][4][5] - заголовок: [4]*256+[5]=длина блока
                        // [6] - адрес устройства (как получено);
                        // [7] - код функции; [8] - количество байт ответа Modbus устройства;
                        // [9]..[n] - данные, для функции 3,4: [8]/2= количество регистров.
                        if ((receivedBytes[4] * 256 + receivedBytes[5] == numBytes - 6) &&
                            receivedBytes[6] == nodeAddr && receivedBytes[7] == funcCode)
                        {
                            var msgReceive = new byte[numBytes];
                            Array.Copy(receivedBytes, msgReceive, numBytes);
                            stream.Write(msgReceive, 0, msgReceive.Length);
                            var items = mif.ReadSectionKeys("Items");
                            ThreadPool.QueueUserWorkItem(arg =>
                            {
                                try
                                {
                                    if (funcCode == 3 || funcCode == 4)
                                    {
                                        var regcount = receivedBytes[8] / 2;
                                        var fetchvals = new ushort[regcount];
                                        var n = 9;
                                        for (var i = 0; i < regcount; i++)
                                        {
                                            var value = Swap(BitConverter.ToUInt16(receivedBytes, n));
                                            var addr = startAddr + i;
                                            var key = $"[{nodeAddr}][{addr}]";
                                            var itemName = items[addr];
                                            if (DictModbusItems.TryGetValue(key, out ushort register))
                                            {
                                                if (value != register)
                                                {
                                                    DictModbusItems.TryUpdate(key, value, register);
                                                    if (mif.KeyExists("Flags", itemName))
                                                        FlagIndication(nodeAddr, itemName, register, value);
                                                    else
                                                        Say = $"[{nodeAddr}] {itemName} {register} -> {value}\t{mif.ReadString("Items",itemName, "")}";
                                                }
                                            }
                                            else
                                            {
                                                if (DictModbusItems.TryAdd(key, value))
                                                {
                                                    if (mif.KeyExists("Flags", itemName))
                                                        FlagIndication(nodeAddr, itemName, value);
                                                    else
                                                        Say = $"[{nodeAddr}] {itemName} ? -> {value}\t{mif.ReadString("Items", itemName, "")}";
                                                }
                                            }
                                            n += 2;
                                        }
                                    }
                                    else if (funcCode == 16)
                                    {
                                        var request = string.Join(",", msgSend.Skip(6));
                                        var answer = string.Join(",", msgReceive.Skip(6));
                                        if (request.StartsWith(answer))
                                        {
                                            var regAddr = Swap(BitConverter.ToUInt16(msgSend, 8));
                                            //var desc = new Tuple<string, string>("", "");
                                            var regCount = Swap(BitConverter.ToUInt16(msgSend, 10));
                                            var n = 13;
                                            for (var i = 0; i < regCount; i++)
                                            {
                                                var value = Swap(BitConverter.ToUInt16(msgSend, n));
                                                var addr = regAddr + i;
                                                var itemName = items[addr];
                                                Say = $"[{nodeAddr}] {itemName} <- {value}\t{mif.ReadString("Items", itemName, "")}";
                                                n += 2;
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Say = ex.Message;
                                }
                            });
                        }
                     }
                    catch (Exception)
                    {

                    }
                }
                catch (Exception)
                {

                }
            }
        }

        private void FlagIndication(byte nodeAddr, string itemName, ushort register, ushort value)
        {
            if (mif.SectionExists(itemName))
            {
                var keys = mif.ReadSectionKeys(itemName);
                for (var k = 0; k < 16; k++)
                {
                    if (mif.KeyExists(itemName, $"{k}"))
                    {
                        var regBit = register & (1 << k);
                        var valBit = value & (1 << k);
                        if (regBit != valBit)
                            Say = $"[{nodeAddr}] {itemName}.{k} {regBit >> k} -> {valBit >> k}\t{mif.ReadString(itemName, $"{k}", "")}";
                    }
                }
            }
        }

        private void FlagIndication(byte nodeAddr, string itemName, ushort value)
        {
            if (mif.SectionExists(itemName))
            {
                var keys = mif.ReadSectionKeys(itemName);
                for (var k = 0; k < 16; k++)
                {
                    if (mif.KeyExists(itemName, $"{k}"))
                    {
                        if ((value & (1 << k)) > 0)
                            Say = $"[{nodeAddr}] {itemName}.{k} ? -> 1\t{mif.ReadString(itemName, $"{k}", "")}";
                    }
                }
            }
        }

        private void EmulatorForm_Load(object sender, EventArgs e)
        {
            _worker.RunWorkerAsync();
        }

        private static ushort Swap(ushort value)
        {
            var bytes = BitConverter.GetBytes(value);
            var buff = bytes[0];
            bytes[0] = bytes[1];
            bytes[1] = buff;
            return BitConverter.ToUInt16(bytes, 0);
        }

        public static ushort Crc(IList<byte> buff, int len)
        {   // контрольная сумма MODBUS RTU
            ushort result = 0xFFFF;
            if (len <= buff.Count)
            {
                for (var i = 0; i < len; i++)
                {
                    result ^= buff[i];
                    for (var j = 0; j < 8; j++)
                    {
                        var flag = (result & 0x0001) > 0;
                        result >>= 1;
                        if (flag) result ^= 0xA001;
                    }
                }
            }
            return result;
        }

        private string Say
        {
            set
            {
                const int maxlines = 31;
                var method = new MethodInvoker(() =>
                    {
                        lbMessages.BeginUpdate();
                        try
                        {
                            lbMessages.Items.Add(String.Format("{0} : {1}", DateTime.Now.ToString("HH:mm:ss.fff"), value));
                            if (lbMessages.Items.Count > maxlines)
                                lbMessages.Items.RemoveAt(0);
                        }
                        finally
                        {
                            lbMessages.EndUpdate();
                        }
                    });
                if (InvokeRequired)
                    BeginInvoke(method);
                else
                    method();
            }
        }

        private void EmulatorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _worker.CancelAsync();
        }

    }
}