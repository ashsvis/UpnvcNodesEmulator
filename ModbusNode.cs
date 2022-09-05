using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace UpnvcNodesEmulator
{
    public class UpnvcNode : ModbusItem
    {
        private readonly ushort[] _hregs = new ushort[100];

        public ushort this[int regNum]
        {
            get
            {
                if (regNum < 0 || regNum > 63) return 0;

                return _hregs[regNum];
            }
            set
            {
                if (regNum < 0 || regNum > 63) return;
                _hregs[regNum] = value;
            }
        }

        public UpnvcNode()
        {
            // читаемые регистры для настройки
            _hregs[0x14] = 9;
            _hregs[0x19] = 10;
            _hregs[0x1A] = 150;
            _hregs[0x1B] = 3000;
            _hregs[0x1C] = 16000;
            _hregs[0x1E] = 21500;
            _hregs[0x1F] = 2500;
            _hregs[0x20] = 20500;
            _hregs[0x21] = 3500;
            _hregs[0x26] = 0;
            _hregs[0x27] = 0;
            _hregs[0x2C] = 0;
            _hregs[0x2D] = 14000;
            _hregs[0x2F] = 45;
            _hregs[0x30] = 3517;
            _hregs[0x31] = 250;
            _hregs[0x32] = 7490;
            _hregs[0x33] = 800;
            _hregs[0x34] = 16760;
            _hregs[0x35] = 1000;
            _hregs[0x36] = 900;
            _hregs[0x37] = 387;
            _hregs[0x38] = 2000;
            _hregs[0x39] = 5000;
            // начальные данные для положения готовности к наливу
            Level = 2250;
            LogicState = NodeLogicState.Wait;
            ReadyLink = true;
            WorkPositionState = GerkonWorkPosition = true;
        }

        public void CalcState()
        {
        	switch (LogicState)
        	{
        		case NodeLogicState.None:
        			// сброс всех состояний и переход в ожидание
        			LogicState = NodeLogicState.Wait;
        			break;
        		case NodeLogicState.Wait:
        			// основное состояние ожидания налива в автоматическом режиме
        			if (OperatorStartFilling)
        			{
        				OperatorStartFilling = false;
        				LogicState = NodeLogicState.FillingBySmallValve;
        			}
        			break;
        		case NodeLogicState.FillingBySmallValve:
        			SmallValveState = true;
        			ProcCommandSmallValve = true;
        			ProcStateSmallValve = true;
        			GerkonSmall = true;
       				LogicState = NodeLogicState.FillingByBigValve;
        			break;
        		case NodeLogicState.FillingByBigValve:
        			BigValveState = true;
        			ProcCommandBigValve = true;
        			ProcStateBigValve = true;
        			GerkonBig = true;
        			ProcStateGreenLamp = true;
        			Level = Level + 33;
        			if (OperatorStopFilling)
        			{
        				OperatorStopFilling = false;
        				LogicState = NodeLogicState.FillingEndingByOperator;
        			}       				
        			else if (Level >= SetLevel)
        			{
        				LogicState = NodeLogicState.FillingEnding;
        			}
        			break;
        		case NodeLogicState.FillingEndingByOperator:
        		case NodeLogicState.FillingEnding:
        			BigValveState = false;
        			ProcCommandBigValve = false;
        			ProcStateBigValve = false;
        			GerkonBig = false;
        			SmallValveState = false;
        			ProcCommandSmallValve = false;
        			ProcStateSmallValve = false;
        			GerkonSmall = false;
        			ProcStateGreenLamp = false;
        			var states = new bool[16];
        			if (LogicState == NodeLogicState.FillingEndingByOperator)
        				states[14] = true;
        			else if (LogicState == NodeLogicState.FillingEnding)
        				states[13] = true;
        			LastStopFlags = states;
        			StopCount++;
        			LogicState = NodeLogicState.FillingEnded;
        			break;
	       		case NodeLogicState.FillingEnded:
        			LogicState = NodeLogicState.Wait;
        			break;        			
        	}
        }
        
        [Category("Настройки уровня")]
        [TypeConverter(typeof(int))]
        [DefaultValue(typeof(int), "0")]
        [DisplayName(@"HR35 DeepLevel")]
        public int DeepLevel
        {
        	get { return (int)_hregs[0x35]; }
        	set { _hregs[0x35] = (ushort)value; }
        }
               
        [Category("Настройки уровня")]
        [TypeConverter(typeof(int))]
        [DefaultValue(typeof(int), "0")]
        [DisplayName(@"HR36 WorkLevel")]
        public int WorkLevel
        {
        	get { return (int)_hregs[0x36]; }
        	set { _hregs[0x36] = (ushort)value; }
        }
               
        [Category("(Задание налива)")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR06.00 OperatorStartFilling")]
        public bool OperatorStartFilling
        {
        	get { return (_hregs[0x06] & 0x0001) > 0; }
        	set 
        	{ 
        		SetHRegFlag(ref _hregs[0x06], 0, value); 
        	}
        }
        
        [Category("(Задание налива)")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR06.01 OperatorStopFilling")]
        public bool OperatorStopFilling
        {
        	get { return (_hregs[0x06] & 0x0002) > 0; }
        	set { SetHRegFlag(ref _hregs[0x06], 1, value); }
        }
        
        [Category("(Задание налива)")]
        [TypeConverter(typeof(int))]
        [DefaultValue(typeof(int), "0")]
        [DisplayName(@"HR07 MaxLevel")]
        public int MaxLevel
        {
        	get { return (int)_hregs[0x07]; }
        	set 
        	{ 
        		_hregs[0x07] = (ushort)value;
				Level = value - DeepLevel;        		
        	}
        }        

        [Category("(Задание налива)")]
        [TypeConverter(typeof(int))]
        [DefaultValue(typeof(int), "0")]
        [DisplayName(@"HR08 SetLevel")]
        public int SetLevel
        {
        	get { return (int)_hregs[0x08]; }
        	set { _hregs[0x08] = (ushort)value; }
        }        
        
        [Category("Текущий уровень в мм")]
        [TypeConverter(typeof(int))]
        [DefaultValue(typeof(int), "0")]
        [DisplayName(@"HR00 Level")]
        public int Level
        {
        	get { return (int)_hregs[0x00]; }
        	set { _hregs[0x00] = (ushort)value; }
        }
        
        [Category("Счетчик остановов налива")]
        [TypeConverter(typeof(byte))]
        [DefaultValue(typeof(byte), "0")]
        [DisplayName(@"HR01.00-07 StopCount")]
        public byte StopCount
        {
        	get { return (byte)(_hregs[0x01] & 0xFF); }
        	set { _hregs[0x01] = (ushort)((_hregs[0x01] & 0xFF00) | value); }
        }
        
        [Category("Состояние текущих сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR01.08 AlarmLevel")]
        public bool AlarmLevel
        {
        	get { return (_hregs[0x01] & 0x0100) > 0; }
        	set { SetHRegFlag(ref _hregs[0x01], 8, value); }
        }

        [Category("Состояние текущих сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR01.10 GroundState")]
        public bool GroundState
        {
        	get { return (_hregs[0x01] & 0x0400) > 0; }
        	set { SetHRegFlag(ref _hregs[0x01], 10, value); }
        }
        
        [Category("Состояние текущих сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR01.11 HandFill")]
        public bool HandFill
        {
        	get { return (_hregs[0x01] & 0x0800) > 0; }
        	set { SetHRegFlag(ref _hregs[0x01], 11, value); }
        }
        
        [Category("Состояние текущих сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR01.12 BigFlow")]
        public bool BigValveState
        {
        	get { return (_hregs[0x01] & 0x1000) > 0; }
        	set { SetHRegFlag(ref _hregs[0x01], 12, value); }
        }
        
        [Category("Состояние текущих сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR01.13 SmallFlow")]
        public bool SmallValveState
        {
        	get { return (_hregs[0x01] & 0x2000) > 0; }
        	set { SetHRegFlag(ref _hregs[0x01], 13, value); }
        }
        
        [Category("Состояние текущих сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR01.14 Throat")]
        public bool WorkPositionState
        {
        	get { return (_hregs[0x01] & 0x4000) > 0; }
        	set { SetHRegFlag(ref _hregs[0x01], 14, value); }
        }
        
        [Category("Состояние текущих сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR01.15 OtherFault")]
        public bool OtherFault
        {
        	get { return (_hregs[0x01] & 0x8000) > 0; }
        	set { SetHRegFlag(ref _hregs[0x01], 15, value); }
        }
   
        [Category("Причины последнего прекращения налива")]
        public bool[] LastStopFlags
        {
            get
            {
                var buff = new bool[16];
                var bits = new BitArray(BitConverter.GetBytes(_hregs[0x02]));
                for (var i = 0; i < 16; i++) buff[i] = bits[i];
                return buff;
            }
            set
            {
                var ival = 0;
                for (var i = 0; i < 16; i++)
                    if (value[i])
                        ival |= 1 << i;
                _hregs[0x02] = Convert.ToUInt16(ival);
            }
        }
        
        [Category("Состояние входных сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR03.00 ButtomStart")]
        public bool ButtomStart
        {
        	get { return (_hregs[0x03] & 0x0001) > 0; }
        	set { SetHRegFlag(ref _hregs[0x03], 0, value); }
        }

        [Category("Состояние входных сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR03.01 GerkonBig")]
        public bool GerkonBig
        {
        	get { return (_hregs[0x03] & 0x0002) > 0; }
        	set { SetHRegFlag(ref _hregs[0x03], 1, value); }
        }
        
        [Category("Состояние входных сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR03.02 GerkonSmall")]
        public bool GerkonSmall
        {
        	get { return (_hregs[0x03] & 0x0004) > 0; }
        	set { SetHRegFlag(ref _hregs[0x03], 2, value); }
        }
        
        [Category("Состояние входных сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR03.03 ReadyLink")]
        public bool ReadyLink
        {
        	get { return (_hregs[0x03] & 0x0008) > 0; }
        	set { SetHRegFlag(ref _hregs[0x03], 3, value); }
        }

        [Category("Состояние входных сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR03.04 GerkonWorkPosition")]
        public bool GerkonWorkPosition
        {
        	get { return (_hregs[0x03] & 0x0010) > 0; }
        	set { SetHRegFlag(ref _hregs[0x03], 4, value); }
        }
        
        [Category("Состояние входных сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR03.05 GroundCheck")]
        public bool GroundCheck
        {
        	get { return (_hregs[0x03] & 0x0020) > 0; }
        	set { SetHRegFlag(ref _hregs[0x03], 5, value); }
        }
        
        [Category("Состояние входных сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR03.06 LevelCheck")]
        public bool LevelCheck
        {
        	get { return (_hregs[0x03] & 0x0040) > 0; }
        	set { SetHRegFlag(ref _hregs[0x03], 6, value); }
        }
        
        [Category("Состояние входных сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR03.07 ButtonAutonomic")]
        public bool ButtonAutonomic
        {
        	get { return (_hregs[0x03] & 0x0080) > 0; }
        	set { SetHRegFlag(ref _hregs[0x03], 7, value); }
        }
        
        [Category("Состояние входных сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR03.10 LevelCurrentMin")]
        public bool LevelCurrentMin
        {
        	get { return (_hregs[0x03] & 0x0400) > 0; }
        	set { SetHRegFlag(ref _hregs[0x03], 10, value); }
        }
        
        [Category("Состояние входных сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR03.11 LevelCurrentMax")]
        public bool LevelCurrentMax
        {
        	get { return (_hregs[0x03] & 0x0800) > 0; }
        	set { SetHRegFlag(ref _hregs[0x03], 11, value); }
        }
        
        [Category("Состояние входных сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR03.14 AlarmCurrentMin")]
        public bool AlarmCurrentMin
        {
        	get { return (_hregs[0x03] & 0x4000) > 0; }
        	set { SetHRegFlag(ref _hregs[0x03], 14, value); }
        }
        
        [Category("Состояние входных сигналов")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR03.15 AlarmCurrentMax")]
        public bool AlarmCurrentMax
        {
        	get { return (_hregs[0x03] & 0x8000) > 0; }
        	set { SetHRegFlag(ref _hregs[0x03], 15, value); }
        }
        
        [Category("Программные флаги")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.00 ProcAlarmLevel")]
        public bool ProcAlarmLevel
        {
        	get { return (_hregs[0x04] & 0x0001) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 0, value); }
        }
        
        [Category("Программные флаги")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.01 ProcButtonStop")]
        public bool ProcButtonStop
        {
        	get { return (_hregs[0x04] & 0x0002) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 1, value); }
        }
        
        [Category("Программные флаги")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.02 ProcFaultGroundLink")]
        public bool ProcFaultGroundLink
        {
        	get { return (_hregs[0x04] & 0x0004) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 2, value); }
        }
        
        [Category("Программные флаги")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.03 ProcFaultAlarmLevel")]
        public bool ProcFaultAlarmLevel
        {
        	get { return (_hregs[0x04] & 0x0008) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 3, value); }
        }
        
        [Category("Программные флаги")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.04 ProcFaultLinkTimeout")]
        public bool ProcFaultLinkTimeout
        {
        	get { return (_hregs[0x04] & 0x0010) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 4, value); }
        }
        
        [Category("Программные флаги")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.05 ProcFaultGround")]
        public bool ProcFaultGround
        {
        	get { return (_hregs[0x04] & 0x0020) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 5, value); }
        }
        
        [Category("Программные флаги")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.06 ProcFaultBigValve")]
        public bool ProcFaultBigValve
        {
        	get { return (_hregs[0x04] & 0x0040) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 6, value); }
        }
        
        [Category("Программные флаги")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.07 ProcFaultSmallValve")]
        public bool ProcFaultSmallValve
        {
        	get { return (_hregs[0x04] & 0x0080) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 7, value); }
        }
        
        [Category("Команды на включение")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.08 ProcCommandBigValve")]
        public bool ProcCommandBigValve
        {
        	get { return (_hregs[0x04] & 0x0100) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 8, value); }
        }
        
        [Category("Команды на включение")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.09 ProcCommandSmallValve")]
        public bool ProcCommandSmallValve
        {
        	get { return (_hregs[0x04] & 0x0200) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 9, value); }
        }
        
        [Category("Программные флаги")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.12 ProcStateBigValve")]
        public bool ProcStateBigValve
        {
        	get { return (_hregs[0x04] & 0x1000) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 12, value); }
        }
        
        [Category("Программные флаги")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.13 ProcStateSmallValve")]
        public bool ProcStateSmallValve
        {
        	get { return (_hregs[0x04] & 0x2000) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 13, value); }
        }
        
        [Category("Команды на включение")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.14 ProcStateGreenLamp")]
        public bool ProcStateGreenLamp
        {
        	get { return (_hregs[0x04] & 0x4000) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 14, value); }
        }
        
        [Category("Команды на включение")]
        [TypeConverter(typeof(bool))]
        [DefaultValue(typeof(bool), "False")]
        [DisplayName(@"HR04.15 ProcStateBlueLamp")]
        public bool ProcStateBlueLamp
        {
        	get { return (_hregs[0x04] & 0x8000) > 0; }
        	set { SetHRegFlag(ref _hregs[0x04], 15, value); }
        }
        
        [Category("Состояние логики")]
        [DisplayName(@"Состояние логики")]
        [TypeConverter(typeof(EnumTypeConverter))]
        [DefaultValue(typeof(NodeLogicState), "Wait")]
        public NodeLogicState LogicState 
        { 
        	get { return (NodeLogicState)_hregs[0x05]; }
        	set { _hregs[0x05] = (ushort)value; }
        }
        
        public override void SaveProperties(NameValueCollection coll)
        {
            base.SaveProperties(coll);
        }

        public override void LoadProperties(NameValueCollection coll)
        {
            base.LoadProperties(coll);
        }

 		private static void SetHRegFlag(ref ushort hreg, int bit, bool value)
        {
            if (value)
                hreg |= (ushort)(0x01 << bit);
            else
                hreg &= (ushort)~(0x01 << bit);
        }        
    }
    
    public enum NodeLogicState
    {
    	None = 0,
    	Wait = 1,
    	WaitAutonomic = 2,
    	FillingBySmallValveAutonomic = 3,
    	FillingByBigValveAutonomic = 4,
    	FillingBySmallValve = 5,
    	FillingByBigValve = 6,
    	FillingEnding = 7,
    	FillingEndingByOperator = 8,
    	FillingEnded = 9
    }
}