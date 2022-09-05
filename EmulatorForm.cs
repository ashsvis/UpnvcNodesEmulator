﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace UpnvcNodesEmulator
{
    public partial class EmulatorForm : Form
    {
        const int socketTimeOut = 3000;
        private readonly BackgroundWorker _worker;

        private static readonly ConcurrentDictionary<string, ModbusItem> DictModbusItems =
            new ConcurrentDictionary<string, ModbusItem>();

        public EmulatorForm()
        {
            InitializeComponent();
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
                                        Exchange(stream, bytes);
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

        private void Exchange(NetworkStream stream, byte[] bytes)
        {
            if (bytes.Length < 8) return;
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
                        sock.Send(bytes);
                        var receivedBytes = new byte[1024];
                        sock.ReceiveTimeout = socketTimeOut;
                        var numBytes = sock.Receive(receivedBytes); //считали numBytes байт
                        sock.Disconnect(true);
                        if ((receivedBytes[4] * 256 + receivedBytes[5] == numBytes - 6) &&
                            receivedBytes[6] == nodeAddr && receivedBytes[7] == funcCode)
                        {
                            var msg = new byte[numBytes];
                            Array.Copy(receivedBytes, msg, numBytes);
                            stream.Write(msg, 0, msg.Length);
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
                const int maxlines = 11;
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