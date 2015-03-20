﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BepopProtocolAnalyzer
{
    public partial class MainForm : Form
    {
        private PacketReader reader;
        private List<Frame> frames;

        private FileStream videoFile;

        private BepopServer s;

        public MainForm()
        {
            InitializeComponent();
            frames = new List<Frame>();
        }

        private void btnOpenPcap_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "PCAP File (*.pcap)|*.pcap";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                lblStatus.Text = "Parsing PCAP...";
                btnOpenPcap.Enabled = false;
                chkShowLL.Enabled = false;
                chkDumpVideo.Enabled = false;
                Task.Factory.StartNew(() =>
                {
                    reader = new PacketReader(ofd.FileName, chkDumpVideo.Checked);
                    reader.OnFrameReceived += reader_OnFrameReceived;
                    reader.OnStreamFinished += reader_OnStreamFinished;
                    reader.OnVideoFrameReceived += reader_OnVideoFrameReceived;
                    reader.Open();
                    reader.Start();
                });
            }
        }

        void reader_OnVideoFrameReceived(object sender, VideoFrameReceived e)
        {
            if (videoFile == null)
            {
                videoFile = File.Create("video.h264");
            }
            videoFile.Write(e.Data, 0, e.Data.Length);
        }

        void reader_OnStreamFinished(object sender, EventArgs e)
        {
            if (videoFile != null)
            {
                videoFile.Flush();
                videoFile.Close();
            }

            var a = new Action(() => lblStatus.Text = "Finished.");
            if (statusStrip1.InvokeRequired)
                statusStrip1.Invoke(a);
            else
                a();
        }

        private void AddFrameToList(Frame f)
        {
            var a = new Action(() =>
            {
                var i = new ListViewItem(f.Time.ToString("0.###"));
                i.SubItems.Add(f.Direction.ToString());
                i.SubItems.Add(f.Type.ToString());

                if(f.Id == 0)
                    i.SubItems.Add("PING");
                else if (f.Id == 1)
                    i.SubItems.Add("PONG");
                else
                    i.SubItems.Add(f.Id.ToString());

                i.SubItems.Add(f.Seq.ToString());
                i.SubItems.Add(f.Data.Length.ToString());
                if (f.Id >= 2 && (f.Type == FrameType.DATA_W_ACK
                    || f.Type == FrameType.DATA_LL
                    || f.Type == FrameType.DATA))
                {
                    var proj = (PacketType)f.Data[0];
                    var c = Packet.GetPacketClass(proj, f.Data[1]);

                    ushort command = 0;
                    command = f.Data[2];
                    command |= (ushort)(f.Data[3] << 8);

                    var cmd = Packet.GetPacketCommand(proj, f.Data[1], command);
                    i.SubItems.Add(proj.ToString());
                    i.SubItems.Add(c);
                    i.SubItems.Add(cmd);
                }
                i.Tag = f;
                lstPackets.Items.Add(i);
            });
            if (lstPackets.InvokeRequired)
                lstPackets.Invoke(a);
            else
                a();
        }

        void reader_OnFrameReceived(object sender, FrameReceivedEventArgs e)
        {
            frames.Add(e.Frame);
            if (e.Frame.Type != FrameType.DATA_LL || (chkShowLL.Checked))
            {
                AddFrameToList(e.Frame);
            }
        }

        private void lstPackets_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstPackets.SelectedItems.Count > 0)
            {
                var selectedFrame = (Frame)lstPackets.SelectedItems[0].Tag;

                if (hxBox.ByteProvider != null)
                {
                    var p = (Be.Windows.Forms.DynamicFileByteProvider)hxBox.ByteProvider;
                    p.Dispose();
                }

                var ms = new MemoryStream(selectedFrame.Data);
                var prov = new Be.Windows.Forms.DynamicFileByteProvider(ms);

                hxBox.ByteProvider = prov;
            }
        }

        private void lstPackets_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (lstPackets.SelectedItems.Count > 0)
            {
                var selectedFrame = (Frame)lstPackets.SelectedItems[0].Tag;
                if (selectedFrame.Type == FrameType.DATA
                    || selectedFrame.Type == FrameType.DATA_LL
                    || selectedFrame.Type == FrameType.DATA_W_ACK)
                {
                    var packet = Packet.Parse(selectedFrame.Data);
                    var form = new PacketInspectorForm(packet);
                    form.ShowDialog();
                }

            }
        }

        private void btnStartSimulator_Click(object sender, EventArgs e)
        {
            if (s == null)
            {
                s = new BepopServer(44444);
                s.OnFrameReceived += reader_OnFrameReceived;
                s.Start();
                btnStartSimulator.Enabled = false;
            }
        }
    }
}
