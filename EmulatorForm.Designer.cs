namespace UpnvcNodesEmulator
{
    partial class EmulatorForm
    {
        /// <summary>
        /// Требуется переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Обязательный метод для поддержки конструктора - не изменяйте
        /// содержимое данного метода при помощи редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tvTree = new System.Windows.Forms.TreeView();
            this.pgProps = new System.Windows.Forms.PropertyGrid();
            this.lbMessages = new System.Windows.Forms.ListBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.cbMute = new System.Windows.Forms.CheckBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 35.79882F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 64.20119F));
            this.tableLayoutPanel1.Controls.Add(this.tvTree, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.pgProps, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.lbMessages, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.panel1, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.Size = new System.Drawing.Size(676, 557);
            this.tableLayoutPanel1.TabIndex = 3;
            // 
            // tvTree
            // 
            this.tvTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tvTree.HideSelection = false;
            this.tvTree.Location = new System.Drawing.Point(3, 3);
            this.tvTree.Name = "tvTree";
            this.tvTree.Size = new System.Drawing.Size(236, 324);
            this.tvTree.TabIndex = 2;
            this.tvTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.tvTree_AfterSelect);
            this.tvTree.MouseDown += new System.Windows.Forms.MouseEventHandler(this.tvTree_MouseDown);
            // 
            // pgProps
            // 
            this.pgProps.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pgProps.LineColor = System.Drawing.SystemColors.ControlDark;
            this.pgProps.Location = new System.Drawing.Point(245, 3);
            this.pgProps.Name = "pgProps";
            this.pgProps.Size = new System.Drawing.Size(428, 324);
            this.pgProps.TabIndex = 3;
            this.pgProps.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.pgProps_PropertyValueChanged);
            // 
            // lbMessages
            // 
            this.lbMessages.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.SetColumnSpan(this.lbMessages, 2);
            this.lbMessages.FormattingEnabled = true;
            this.lbMessages.ItemHeight = 17;
            this.lbMessages.Location = new System.Drawing.Point(3, 363);
            this.lbMessages.Name = "lbMessages";
            this.lbMessages.Size = new System.Drawing.Size(670, 191);
            this.lbMessages.TabIndex = 0;
            // 
            // panel1
            // 
            this.panel1.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.panel1, 2);
            this.panel1.Controls.Add(this.cbMute);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(2, 332);
            this.panel1.Margin = new System.Windows.Forms.Padding(2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(672, 26);
            this.panel1.TabIndex = 4;
            // 
            // cbMute
            // 
            this.cbMute.AutoSize = true;
            this.cbMute.Dock = System.Windows.Forms.DockStyle.Left;
            this.cbMute.Location = new System.Drawing.Point(0, 0);
            this.cbMute.Name = "cbMute";
            this.cbMute.Padding = new System.Windows.Forms.Padding(5, 0, 0, 0);
            this.cbMute.Size = new System.Drawing.Size(103, 26);
            this.cbMute.TabIndex = 6;
            this.cbMute.Text = "не отвечать";
            this.cbMute.UseVisualStyleBackColor = true;
            this.cbMute.CheckedChanged += new System.EventHandler(this.cbMute_CheckedChanged);
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 1667;
            this.timer1.Tick += new System.EventHandler(this.Timer1Tick);
            // 
            // EmulatorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(676, 557);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "EmulatorForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Эмулятор протокола MODBUS RTU для стояков налива ПТХН";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.EmulatorForm_FormClosing);
            this.Load += new System.EventHandler(this.EmulatorForm_Load);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }
        private System.Windows.Forms.Timer timer1;

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TreeView tvTree;
        private System.Windows.Forms.PropertyGrid pgProps;
        private System.Windows.Forms.ListBox lbMessages;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.CheckBox cbMute;
    }
}

