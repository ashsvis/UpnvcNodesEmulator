using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
//using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

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
        }

        private void EmulatorForm_Load(object sender, EventArgs e)
        {
            cbPort.Items.Add("TCP");
            cbPort.Text = cbPort.Items[0].ToString();
            //--------------------
            cbPort.SelectionChangeCommitted -= cbPort_SelectionChangeCommitted;
            nudPort.ValueChanged -= nudPort_ValueChanged;
            try
            {
                LoadProperties();
                nudPort.Visible = true;
            }
            finally
            {
                cbPort.SelectionChangeCommitted += cbPort_SelectionChangeCommitted;
                nudPort.ValueChanged += nudPort_ValueChanged;
            }
            _worker.DoWork += (o, args) =>
                {
                    var worker = (BackgroundWorker) o;

                    #region работа с TCP портом

                    var tt = args.Argument as TcpTuning;
                    if (tt != null)
                    {
                        const int socketTimeOut = 3000;
                        var listener = new TcpListener(IPAddress.Any, tt.Port)
                            {
                                Server = {SendTimeout = socketTimeOut, ReceiveTimeout = socketTimeOut}
                            };
                        Say = String.Format("Сокет TCP({0}) прослушивается...", tt.Port);
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
                                    Say = String.Format("Сокет TCP({0}) - остановка прослушивания.", tt.Port);
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
                                                var list = new List<string>();
                                                for (var i = 0; i < count; i++) list.Add(String.Format("{0}", bytes[i]));
                                                Say = "Q:" + String.Join(",", list);

                                                if (count < 6) continue;
                                                var header1 = Convert.ToUInt16(bytes[0] * 256 + bytes[1]);
                                                var header2 = Convert.ToUInt16(bytes[2] * 256 + bytes[3]);
                                                var packetLen = Convert.ToUInt16(bytes[4] * 256 + bytes[5]);
                                                if (count != packetLen + 6) continue;
                                                var nodeAddr = bytes[6];
                                                var funcCode = bytes[7];
                                                var startAddr = Convert.ToUInt16(bytes[8] * 256 + bytes[9]);
                                                var regCount = Convert.ToUInt16(bytes[10] * 256 + bytes[11]);
                                                EnsureNode(nodeAddr);
                                                var nodeName = String.Format("Node{0}", nodeAddr);
                                                ModbusItem modbusitem;
                                                while (!DictModbusItems.TryGetValue(nodeName, out modbusitem)) Thread.Sleep(10);
                                                var modbusNode = (UpnvcNode)modbusitem;
                                                var nodeMute = !modbusNode.Active;
                                                List<byte> answer;
                                                switch (funcCode)
                                                {
                                                    case 3: // - read holding registers
                                                    case 4: // - read input registers
                                                        answer = new List<byte>();
                                                        answer.AddRange(BitConverter.GetBytes(Swap(header1)));
                                                        answer.AddRange(BitConverter.GetBytes(Swap(header2)));
                                                        var bytesCount = Convert.ToByte(regCount*2);
                                                        packetLen = Convert.ToUInt16(bytesCount + 3); // 
                                                        answer.AddRange(BitConverter.GetBytes(Swap(packetLen)));
                                                        answer.Add(nodeAddr);
                                                        answer.Add(funcCode);
                                                        answer.Add(bytesCount);
                                                        for (var addr = 0; addr < regCount; addr++)
                                                        {
                                                            EnsureModbusHr(nodeAddr, startAddr + addr);
                                                            var value = modbusNode[startAddr + addr];
                                                            answer.AddRange(BitConverter.GetBytes(Swap(value)));
                                                        }
                                                        lock (Mutelock)
                                                        {
                                                            if (!_mute && !nodeMute)
                                                            {
                                                                list.Clear();
                                                                list.AddRange(
                                                                    answer.Select(t => String.Format("{0}", t)));
                                                                Say = "A:" + String.Join(",", list);
                                                                var msg = answer.ToArray();
                                                                stream.Write(msg, 0, msg.Length);
                                                            }
                                                        }
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
                                                        if (bytesToWrite != regCount*2) break;
                                                        var n = 13;
                                                        for (var i = 0; i < regCount; i++)
                                                        {
                                                            var value = Convert.ToUInt16(bytes[n]*256 + bytes[n + 1]);
                                                            EnsureModbusHr(nodeAddr, startAddr + i);
                                                            modbusNode[startAddr + i] =
                                                                BitConverter.ToUInt16(BitConverter.GetBytes(value),
                                                                                      0);
                                                            while (
                                                                !DictModbusItems.TryUpdate(nodeName, modbusitem,
                                                                                           modbusitem))
                                                                Thread.Sleep(10);
                                                            n = n + 2;
                                                        }
                                                        lock (Mutelock)
                                                        {
                                                            if (!_mute && !nodeMute)
                                                            {
                                                                list.Clear();
                                                                list.AddRange(
                                                                    answer.Select(t => String.Format("{0}", t)));
                                                                Say = "A:" + String.Join(",", list);
                                                                var msg = answer.ToArray();
                                                                stream.Write(msg, 0, msg.Length);
                                                            }
                                                        }
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
                        Say = String.Format("Сокет TCP({0}) - остановка прослушивания.", tt.Port);
                    }

                    #endregion работа с TCP портом

                };
            ReopenPort();
        }

        private static ushort Swap(ushort value)
        {
            var bytes = BitConverter.GetBytes(value);
            var buff = bytes[0];
            bytes[0] = bytes[1];
            bytes[1] = buff;
            return BitConverter.ToUInt16(bytes, 0);
        }

        private void LoadProperties()
        {
            var filename = Path.ChangeExtension(
                Application.ExecutablePath, ".ini");
            if (!File.Exists(filename)) return;
            var lines = File.ReadAllLines(filename);
            var section = "";
            foreach (var line in lines.Where(line => line.Trim().Length != 0))
            {
                if (line.Trim().StartsWith("[") && line.Trim().EndsWith("]"))
                    section = line.Trim(new[] {'[', ']'});
                else
                {
                    var arr = line.Split(new[] {'='});
                    if (arr.Length == 2)
                    {
                        var param = arr[0];
                        var value = arr[1];
                        switch (section)
                        {
                            case "Listening":
                                switch (param)
                                {
                                    case "PortName":
                                        cbPort.Text = value;
                                        break;
                                    case "EthernetPort":
                                        int port;
                                        if (int.TryParse(value, out port))
                                            nudPort.Value = port;
                                        break;
                                }
                                break;
                        }
                    }
                }
            }
        }

        private void SaveProperties()
        {
            var lines = new List<string>
                {
                    "[Listening]",
                    "PortName=" + cbPort.Text,
                    "EthernetPort=" + Convert.ToInt32(nudPort.Value).ToString("0"),
                    ""
                };
            File.WriteAllLines(Path.ChangeExtension(
                Application.ExecutablePath, ".ini"), lines);
        }

        private void cbPort_SelectionChangeCommitted(object sender, EventArgs e)
        {
            cbPort.SelectionChangeCommitted -= cbPort_SelectionChangeCommitted;
            nudPort.Visible = true;
            ReopenPort();
            cbPort.SelectionChangeCommitted += cbPort_SelectionChangeCommitted;
        }

        private void dudBaudRate_SelectedItemChanged(object sender, EventArgs e)
        {
            ReopenPort();
        }

        private void nudPort_ValueChanged(object sender, EventArgs e)
        {
            nudPort.ValueChanged -= nudPort_ValueChanged;
            ReopenPort();
            nudPort.ValueChanged += nudPort_ValueChanged;
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
        private readonly List<UpnvcNode> _nodes = new List<UpnvcNode>();
        
        private void EnsureNode(byte nodeAddr)
        {
            var method = new MethodInvoker(() =>
                {
                    var nodeName = "Node" + nodeAddr;
                    var nodeText = "Контроллер " + nodeAddr.ToString("D2");
                    var nodes = tvTree.Nodes.Find(nodeName, false);
                    if (nodes.Length != 0) return;
                    var childName = String.Format("Node{0}", nodeAddr);
                    var unode = new UpnvcNode {Key = childName};
                    lock (Nodelocker)
                    {
                        _nodes.Add(unode);
                    }
                    DictModbusItems.TryAdd(childName, unode);
                    var node = new TreeNode
                        {
                            Name = nodeName,
                            Text = nodeText,
                            Tag = DictModbusItems
                        };
                    tvTree.BeginUpdate();
                    try
                    {
                        tvTree.Nodes.Add(node);
                        tvTree.Sort();
                    }
                    finally
                    {
                        tvTree.EndUpdate();
                    }
                });
            if (InvokeRequired)
                BeginInvoke(method);
            else
                method();
        }

        private void EnsureModbusHr(byte nodeAddr, int addr)
        {
            EnsureNode(nodeAddr);
            var method = new MethodInvoker(() =>
                {
                    var nodeName = "Node" + nodeAddr;
                    var childName = String.Format("Node{0}.HR{1}", nodeAddr, addr);
                    var nodes = tvTree.Nodes.Find(nodeName, false);
                    if (nodes.Length == 0) return;
                    if (nodes[0].Nodes.Find(childName, false).Length > 0) return;
                    DictModbusItems.TryAdd(childName, new ModbusHoldingRegister { Key = childName });
                });
            if (InvokeRequired)
                BeginInvoke(method);
            else
                method();
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

        private void ReopenPort()
        {
            _worker.CancelAsync();
            while (_worker.IsBusy) Application.DoEvents();
            var portName = cbPort.Text;
            switch (portName)
            {
                case "TCP":
                    {
                        var tcptuning = new TcpTuning
                            {
                                Port = Convert.ToInt32(nudPort.Value)
                            };
                        _worker.RunWorkerAsync(tcptuning);
                    }
                    break;
                case "UDP":
                    {
                        var udptuning = new UdpTuning
                            {
                                Port = Convert.ToInt32(nudPort.Value)
                            };
                        _worker.RunWorkerAsync(udptuning);
                    }
                    break;
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
            while (_worker.IsBusy) Application.DoEvents();
            SaveProperties();
        }

        private void tsmiClear_Click(object sender, EventArgs e)
        {
            if (tvTree.Nodes.Count > 0 &&
                MessageBox.Show(@"Удалить текущие накопленные данные?",
                                @"Очистка текущих накопленных данных",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
                DialogResult.Yes)
            {
                //DictFablItems.Clear();
                tvTree.Nodes.Clear();
                Say = "Текущие накопленные данные удалены.";
            }
        }

        private void tsmiLoad_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;
            Cursor = Cursors.WaitCursor;
            try
            {
                LoadFile(openFileDialog1.FileName);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void LoadFile(string filename)
        {
            if (!File.Exists(filename)) return;
            pgProps.SelectedObject = null;
            if (tvTree.Nodes.Count > 0 &&
                MessageBox.Show(@"Удалить текущие накопленные данные?",
                                @"Загрузка ранее сохранённых данных",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
                DialogResult.Yes)
            {
                tvTree.Nodes.Clear();
                Say = "Текущие накопленные данные удалены.";
            }
            var lines = File.ReadAllLines(filename);
            var coll = new NameValueCollection();
            foreach (var line in lines)
            {
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    coll.Clear();
                    var key = line.Trim(new[] {'[', ']'});
                    var names = key.Split(new[] {'.'});
                    var node = names[0];
                    byte inode;
                    if (node.StartsWith("Node") &&
                        byte.TryParse(node.Substring(4), out inode))
                    {
                        switch (names.Length)
                        {
                            case 3:
                                {
                                    var alg = names[1];
                                    ushort ialg;
                                    if (alg.StartsWith("Alg") &&
                                        ushort.TryParse(alg.Substring(3), out ialg))
                                    {
                                        var name = names[2];
                                        byte ipar, iout;
                                        if (name.StartsWith("Par") &&
                                            byte.TryParse(name.Substring(3), out ipar))
                                        {
                                            coll.Add("Key", String.Format("Node{0}.Alg{1}.Par{2}", inode, ialg, ipar));
                                        }
                                        else if (name.StartsWith("Out") &&
                                                 byte.TryParse(name.Substring(3), out iout))
                                        {
                                            coll.Add("Key", String.Format("Node{0}.Alg{1}.Out{2}", inode, ialg, iout));
                                        }
                                    }
                                }
                                break;
                            case 2:
                                var kontur = names[1];
                                var sinr = names[1];
                                byte ikontur;
                                byte inr;
                                if (kontur.StartsWith("Kontur") &&
                                    byte.TryParse(kontur.Substring(6), out ikontur))
                                {
                                    coll.Add("Key", String.Format("Node{0}.Kontur{1}", inode, ikontur));
                                    break;
                                }
                                if (sinr.StartsWith("INR") &&
                                    byte.TryParse(sinr.Substring(3), out inr))
                                {
                                    coll.Add("Key", String.Format("Node{0}.INR{1}", inode, inr));
                                    break;
                                }
                                if (sinr.StartsWith("KD") &&
                                    byte.TryParse(sinr.Substring(2), out inr))
                                {
                                    coll.Add("Key", String.Format("Node{0}.KD{1}", inode, inr));
                                    break;
                                }
                                Say = names[1];
                                break;
                            case 1:
                                coll.Add("Key", String.Format("Node{0}", inode));
                                break;
                        }
                    }
                }
                else if (line.Trim().Length > 0)
                {
                    var values = line.Split(new[] {'='});
                    coll.Add(values[0], values.Length == 2 ? values[1] : "");
                }
                else if (coll.Count > 1)
                {
                    var key = coll["Key"] ?? "";
                }
            }
            Say = "Ранее накопленный данные загружены.";
        }

        private void tsmiSave_Click(object sender, EventArgs e)
        {
            SaveFile(Path.ChangeExtension(Application.ExecutablePath, ".tree"));
        }

        private void SaveFile(string filename)
        {
            var name = filename;
            var lines = new List<string>();
            File.WriteAllLines(name, lines.ToArray(), System.Text.Encoding.UTF8);
            Say = "Текущие накопленные данные сохранены.";
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            var node = tvTree.SelectedNode;
            tsmiDeleteSplitter.Visible = tsmiDelete.Visible = node != null;
        }

        private void tsmiDelete_Click(object sender, EventArgs e)
        {
            var node = tvTree.SelectedNode;
            if (node == null) return;
        }
        
        void Timer1Tick(object sender, EventArgs e)
        {
        	lock (Nodelocker)
        	{
        		foreach (var node in _nodes)
        		{
        			node.CalcState();
        		}
        	}
            
        }

    }
}