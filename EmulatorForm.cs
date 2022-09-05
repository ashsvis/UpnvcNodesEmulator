using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;

namespace UpnvcNodesEmulator
{
    public partial class EmulatorForm : Form
    {
        private readonly BackgroundWorker _worker;

        private static readonly ConcurrentDictionary<string, ModbusItem> DictModbusItems =
            new ConcurrentDictionary<string, ModbusItem>();

        private static readonly object Mutelock = new object();
        private static bool _mute;

        public EmulatorForm()
        {
            InitializeComponent();
            _worker = new BackgroundWorker {WorkerSupportsCancellation = true};
            _worker.DoWork += (o, args) =>
            {
                var worker = (BackgroundWorker)o;

                #region работа с TCP портом

                    const int socketTimeOut = 3000;
                    var listener = new TcpListener(IPAddress.Any, 502)
                    {
                        Server = { SendTimeout = socketTimeOut, ReceiveTimeout = socketTimeOut }
                    };
                    Say = String.Format("Сокет TCP({0}) прослушивается...", 502);
                    do
                    {
                        Thread.Sleep(1);
                        try
                        {
                            listener.Start(10);
                            // Buffer for reading data
                            var bytes = new Byte[256];

                            while (!listener.Pending())
                            {
                                Thread.Sleep(1);
                                if (!worker.CancellationPending) continue;
                                listener.Stop();
                                args.Cancel = true;
                                Say = String.Format("Сокет TCP({0}) - остановка прослушивания.", 502);
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
                                        Thread.Sleep(1);
                                        //var list = new List<string>();
                                        //for (var i = 0; i < count; i++) list.Add(String.Format("{0}", bytes[i]));
                                        //Say = "Q:" + String.Join(",", list);

                                        if (count < 6) continue;
                                        var header1 = Convert.ToUInt16(bytes[0] * 256 + bytes[1]);
                                        var header2 = Convert.ToUInt16(bytes[2] * 256 + bytes[3]);
                                        var packetLen = Convert.ToUInt16(bytes[4] * 256 + bytes[5]);
                                        if (count != packetLen + 6) continue;
                                        var nodeAddr = bytes[6];
                                        var funcCode = bytes[7];
                                        var startAddr = Convert.ToUInt16(bytes[8] * 256 + bytes[9]);
                                        var regCount = Convert.ToUInt16(bytes[10] * 256 + bytes[11]);
                                        var nodeName = String.Format("Node{0}", nodeAddr);
                                        if (!DictModbusItems.TryGetValue(nodeName, out ModbusItem modbusitem))
                                        {
                                            modbusitem = new UpnvcNode() { Key = nodeName };
                                            DictModbusItems.TryAdd(nodeName, modbusitem);
                                        }
                                        var modbusNode = (UpnvcNode)modbusitem;
                                        modbusNode.CalcState();
                                        var nodeMute = !modbusNode.Active;
                                        List<byte> answer;
                                        byte[] msg;
                                        switch (funcCode)
                                        {
                                            case 3: // - read holding registers
                                            case 4: // - read input registers
                                                answer = new List<byte>();
                                                answer.AddRange(BitConverter.GetBytes(Swap(header1)));
                                                answer.AddRange(BitConverter.GetBytes(Swap(header2)));
                                                var bytesCount = Convert.ToByte(regCount * 2);
                                                packetLen = Convert.ToUInt16(bytesCount + 3); // 
                                                answer.AddRange(BitConverter.GetBytes(Swap(packetLen)));
                                                answer.Add(nodeAddr);
                                                answer.Add(funcCode);
                                                answer.Add(bytesCount);
                                                for (var addr = 0; addr < regCount; addr++)
                                                {
                                                    //EnsureModbusHr(nodeAddr, startAddr + addr);
                                                    var value = modbusNode[startAddr + addr];
                                                    answer.AddRange(BitConverter.GetBytes(Swap(value)));
                                                }
                                                msg = answer.ToArray();
                                                stream.Write(msg, 0, msg.Length);
                                                //lock (Mutelock)
                                                //{
                                                //    if (!_mute && !nodeMute)
                                                //    {
                                                //        list.Clear();
                                                //        list.AddRange(
                                                //            answer.Select(t => String.Format("{0}", t)));
                                                //        Say = "A:" + String.Join(",", list);
                                                //        var msg = answer.ToArray();
                                                //        stream.Write(msg, 0, msg.Length);
                                                //    }
                                                //}
                                                break;
                                            case 16: // write several registers
                                                answer = new List<byte>();
                                                answer.AddRange(BitConverter.GetBytes(Swap(header1)));
                                                answer.AddRange(BitConverter.GetBytes(Swap(header2)));
                                                answer.AddRange(BitConverter.GetBytes(Swap(6)));
                                                answer.Add(nodeAddr);
                                                answer.Add(funcCode);
                                                answer.AddRange(BitConverter.GetBytes(Swap(startAddr)));
                                                answer.AddRange(BitConverter.GetBytes(Swap(regCount)));
                                                var bytesToWrite = bytes[12];
                                                if (bytesToWrite != regCount * 2) break;
                                                var n = 13;
                                                for (var i = 0; i < regCount; i++)
                                                {
                                                    var value = Convert.ToUInt16(bytes[n] * 256 + bytes[n + 1]);
                                                    //EnsureModbusHr(nodeAddr, startAddr + i);
                                                    modbusNode[startAddr + i] = BitConverter.ToUInt16(BitConverter.GetBytes(value), 0);
                                                    if (DictModbusItems.TryGetValue(nodeName, out ModbusItem modbusItem))
                                                        DictModbusItems.TryUpdate(nodeName, modbusNode, modbusitem);
                                                    n = n + 2;
                                                }
                                                msg = answer.ToArray();
                                                stream.Write(msg, 0, msg.Length);
                                                //lock (Mutelock)
                                                //{
                                                //    if (!_mute && !nodeMute)
                                                //    {
                                                //        list.Clear();
                                                //        list.AddRange(
                                                //            answer.Select(t => String.Format("{0}", t)));
                                                //        Say = "A:" + String.Join(",", list);
                                                //        var msg = answer.ToArray();
                                                //        stream.Write(msg, 0, msg.Length);
                                                //    }
                                                //}
                                                break;
                                        }
                                    }
                                    // Shutdown and end connection
                                    clientData.Close();
                                }
                                catch (Exception ex)
                                {
                                    //throw new Exception(ex.Message);
                                    if (!worker.CancellationPending) Say = "Ошибка: " + ex.Message;
                                }
                            });
                        }
                        catch (SocketException exception)
                        {
                            if (!worker.CancellationPending)
                                Say = String.Format("Ошибка приёма: {0}", exception.Message);
                            break;
                        }
                    } while (!worker.CancellationPending);
                    listener.Stop();
                    Say = String.Format("Сокет TCP({0}) - остановка прослушивания.", 502);

                #endregion работа с TCP портом

            };
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

        private void cbMute_CheckedChanged(object sender, EventArgs e)
        {
            lock (Mutelock)
            {
                _mute = cbMute.Checked;
            }
        }

        private static readonly object Nodelocker = new object();
        //private readonly List<UpnvcNode> _nodes = new List<UpnvcNode>();
        
        private void EnsureNode(byte nodeAddr)
        {
            var childName = string.Format("Node{0}", nodeAddr);
            var unode = new UpnvcNode { Key = childName };
            //lock (Nodelocker)
            //{
            //    _nodes.Add(unode);
            //}
            DictModbusItems.TryAdd(childName, unode);
            //var method = new MethodInvoker(() =>
            //    {
            //        var nodeName = "Node" + nodeAddr;
            //        //var nodeText = "Контроллер " + nodeAddr.ToString("D2");
            //        var nodes = tvTree.Nodes.Find(nodeName, false);
            //        if (nodes.Length != 0) return;
            //        var childName = String.Format("Node{0}", nodeAddr);
            //        var unode = new UpnvcNode {Key = childName};
            //        lock (Nodelocker)
            //        {
            //            _nodes.Add(unode);
            //        }
            //        DictModbusItems.TryAdd(childName, unode);
            //        //var node = new TreeNode
            //        //    {
            //        //        Name = nodeName,
            //        //        Text = nodeText,
            //        //        Tag = DictModbusItems
            //        //    };
            //        //tvTree.BeginUpdate();
            //        //try
            //        //{
            //        //    tvTree.Nodes.Add(node);
            //        //    tvTree.Sort();
            //        //}
            //        //finally
            //        //{
            //        //    tvTree.EndUpdate();
            //        //}
            //    });
            //if (InvokeRequired)
            //    BeginInvoke(method);
            //else
            //    method();
        }

        private void EnsureModbusHr(byte nodeAddr, int addr)
        {
            EnsureNode(nodeAddr);
            var childName = string.Format("Node{0}.HR{1}", nodeAddr, addr);
            DictModbusItems.TryAdd(childName, new ModbusHoldingRegister { Key = childName });
            //var method = new MethodInvoker(() =>
            //    {
            //        var nodeName = "Node" + nodeAddr;
            //        var childName = String.Format("Node{0}.HR{1}", nodeAddr, addr);
            //        //var nodes = tvTree.Nodes.Find(nodeName, false);
            //        //if (nodes.Length == 0) return;
            //        //if (nodes[0].Nodes.Find(childName, false).Length > 0) return;
            //        DictModbusItems.TryAdd(childName, new ModbusHoldingRegister { Key = childName });
            //    });
            //if (InvokeRequired)
            //    BeginInvoke(method);
            //else
            //    method();
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

        private void tvTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag == DictModbusItems)
            {
                ModbusItem fablitem;
                if (DictModbusItems.TryGetValue(e.Node.Name, out fablitem))
                    pgProps.SelectedObject = fablitem;
            }
            else
                pgProps.SelectedObject = null;
        }

        private void pgProps_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            pgProps.Refresh();
        }

        private void tvTree_MouseDown(object sender, MouseEventArgs e)
        {
            var node = tvTree.GetNodeAt(e.Location);
            tvTree.SelectedNode = node;
            if (node == null)
                pgProps.SelectedObject = null;
        }

        private void EmulatorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _worker.CancelAsync();
        }
        
        void Timer1Tick(object sender, EventArgs e)
        {
        	//lock (Nodelocker)
        	//{
        	//	foreach (var node in _nodes)
        	//	{
        	//		node.CalcState();
        	//	}
        	//}
            
        }

    }
}