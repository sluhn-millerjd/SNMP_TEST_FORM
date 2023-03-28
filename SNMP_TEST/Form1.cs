using System;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using SnmpSharpNet;

namespace SNMP_TEST
{
    public partial class Form1 : Form
    {
        protected Socket _socket;
        protected byte[] _inbuffer;
        protected IPEndPoint _peerIP;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.CheckBox startCheckBox;

        public Form1()
        {
            // it is not neccesary to initialize variables to null, but better safe then sorry
            _socket = null;
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.startCheckBox = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            //
            // listbox1
            //
            this.listBox1.Anchor = ((System.Windows.Forms.AnchorStyles)
                ((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.Location = new System.Drawing.Point(13, 13);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(328, 368);
            this.listBox1.TabIndex = 0;
            //
            // startCheckBox
            //
            this.startCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)
                ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.startCheckBox.Appearance = System.Windows.Forms.Appearance.Button;
            this.startCheckBox.Location = new System.Drawing.Point(347, 12);
            this.startCheckBox.Name = "startCheckBox";
            this.startCheckBox.Size = new System.Drawing.Size(75, 24);
            this.startCheckBox.TabIndex = 3;
            this.startCheckBox.Text = "&Start";
            this.startCheckBox.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.startCheckBox.UseVisualStyleBackColor = true;
            this.startCheckBox.CheckedChanged += new System.EventHandler(this.onStartChanged);
            this.ResumeLayout(false);
            //
            // Form1
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(434, 391);
            this.Controls.Add(this.listBox1);
            this.Controls.Add(this.startCheckBox);
            this.Name = "Form1";
            this.Text = "Form1";
            InitializeComponent();
        }

        private void onStartChanged(object? sender, EventArgs e)
        {
            if (startCheckBox.Checked)
            {
                if (!InitializeReceiver())
                {
                    // unable to start TRAP receiver
                    startCheckBox.Checked = false;
                    return;
                } else
                {
                    startCheckBox.Text = "S&top";
                }
            } else
            {
                StopReceiver();
                startCheckBox.Text = "&Start";
            }
        }

        private void StopReceiver()
        {
            if(_socket != null)
            {
                _socket.Close();
                _socket = null;
            }
        }

        private bool InitializeReceiver()
        {
            if(_socket != null)
            {
                StopReceiver();
            }
            try
            {
                //create an IP/UDP socket
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            } catch (Exception ex)
            {
                listBox1.Items.Add("SNMP trap receiver socket initialization failed with erros: " + ex.Message);
                // there is no need to close the socket because it was never correctly created
                _socket = null;
            }
            if(_socket == null)
                return false;
            try
            {
                // prepare to "bindd" the socket to the local port number
                // binding notifies the operating system that application
                // wishies to receive data sent to the specified port number
                // prepare EndPoint that will bind the application to all available
                //IP address and port 162 (snmp-trap)
                EndPoint localEP = new IPEndPoint(IPAddress.Any, 162);
                // bidn socket
                _socket.Bind(localEP);
            } catch(Exception ex)
            {
                listBox1.Items.Add("SNMP trap receiver initialization failed with error: " + ex.Message);
                _socket.Close();
                _socket = null;
            }
            if(_socket == null)
                return false;
            if(!RegisterReceiverOperation())
                return false;
            return true;
        }

        private bool RegisterReceiverOperation()
        {
            if (_socket == null) return false;
            //socket has been closed
            try
            {
                _peerIP = new IPEndPoint(IPAddress.Any, 0);
                //receive from anybody
                EndPoint ep = (EndPoint)_peerIP;
                _inbuffer = new byte[64 * 1024];
                //nice and big receive buffer
                _socket.BeginReceiveFrom(_inbuffer, 0, 64 * 1024,
                    SocketFlags.None, ref ep, new AsyncCallback(ReceiveCallback),_socket);
            } catch (Exception ex)
            {
                listBox1.Items.Add("Registering receive operation failed with message: " + ex.Message);
                _socket.Close();
                _socket = null;
            }
            if (_socket == null) return false;
            return true;
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            // get a reference to the socket. this is handy if the socket has been closed elsewhere in the class
            Socket sock = (Socket)result.AsyncState;
            _peerIP = new IPEndPoint(IPAddress.Any, 0);
            //variable to store received data length
            int inlen;
            try
            {
                EndPoint ep = (EndPoint)_peerIP;
                inlen = sock.EndReceiveFrom(result, ref ep);
                _peerIP = (IPEndPoint)ep; 
            } catch (Exception ex)
            {
                // only post messages if class socket reference is not null
                // in all other cases, users has terminated the socket
                if (_socket != null)
                {
                    PostAsyncMessage("Receive operation failed with message: " + ex.Message);
                }
                inlen = -1;
            }
            // if socket has been closed, ignore received data and return
            if (_socket == null) { return; }

            //check that received data is long enough
            if (inlen <= 0)
            {
                // request next packet
                RegisterReceiverOperation();
                return;
            }
            int packetVersion = SnmpPacket.GetProtocolVersion(_inbuffer, inlen);
            if (packetVersion == (int)SnmpVersion.Ver1)
            {
                SnmpV1TrapPacket pkt = new SnmpV1TrapPacket();
                try
                {
                    pkt.decode(_inbuffer, inlen);
                } catch (Exception ex)
                {
                    PostAsyncMessage("Error parsing SNMPv1 Trap: " + ex.Message);
                    pkt = null;
                }
                if(pkt != null)
                {
                    PostAsyncMessage(String.Format("*** SNMPv1 TRAP from {0}",_peerIP.ToString()));
                    PostAsyncMessage(String.Format("*** community {0} generic id: {1} specific id: {2}",
                        pkt.Community, pkt.Pdu.Generic, pkt.Pdu.Specific));
                    PostAsyncMessage(string.Format("*** PDU count: {0}", pkt.Pdu.VbCount));
                    foreach (Vb vb in pkt.Pdu.VbList)
                    {
                        PostAsyncMessage(
                            String.Format("**** Vb oid: {0} type: {1} value: {2}",
                            vb.Oid.ToString(), SnmpConstants.GetTypeName(vb.Value.Type), vb.Value.ToString()));
                    }
                    PostAsyncMessage("** End of SNMPv1 TRAP");
                }
            } else if(packetVersion == (int)SnmpVersion.Ver2)
            {
                SnmpV2Packet pkt = new SnmpV2Packet();
                try
                {
                    pkt.decode(_inbuffer, inlen);
                } catch (Exception ex)
                {
                    PostAsyncMessage("Error parsing SNMPv2 TRAP" + ex.Message);
                    pkt = null;
                }
                if(pkt != null)
                {
                    if(pkt.Pdu.Type == PduType.V2Trap)
                    {
                        PostAsyncMessage(String.Format("** SNMPv2 TRAP from {0}", _peerIP.ToString())); 
                    } else if (pkt.Pdu.Type == PduType.Inform)
                    {
                        PostAsyncMessage(string.Format("** SNMPv2 INFORM from {0}", _peerIP.ToString()));
                    } else
                    {
                        PostAsyncMessage(string.Format("Invalid SNMPv2 packet from {0}", _peerIP.ToString()));
                        pkt = null;
                    }
                    if (pkt != null)
                    {
                        PostAsyncMessage(
                            String.Format("*** commumity {0} sysUpTime: {1} trapObjectID: {2}",
                            pkt.Community, pkt.Pdu.TrapSysUpTime, pkt.Pdu.TrapObjectID.ToString()));
                        PostAsyncMessage(String.Format("** PDU count: {0}", pkt.Pdu.VbCount));
                        foreach (Vb vb in pkt.Pdu.VbList)
                        {
                            PostAsyncMessage(
                                String.Format("**** Vb oid: {0} type: {1} value: {2}",
                                vb.Oid.ToString(), SnmpConstants.GetTypeName(vb.Value.Type), vb.Value.ToString()));
                        }
                        if (pkt.Pdu.Type == PduType.V2Trap)
                        {
                            PostAsyncMessage("** End of SNMPv2 TRAP");
                        } else
                        {
                            PostAsyncMessage("** End of SNMPv2 INFORM");
                            //sned ACK bak to INFORM sender
                            SnmpV2Packet response = pkt.BuildInformResponse();
                            byte[] buf = response.encode();
                            _socket.SendTo(buf, (EndPoint)_peerIP);
                        }
                    }
                }
            }
            RegisterReceiverOperation();
        }
        protected delegate void PostAsyncMessageDelegate(string msg);
        private void PostAsyncMessage(string msg)
        {
            if (InvokeRequired)
            {
                Invoke(new PostAsyncMessageDelegate(PostAsyncMessage), new object[] { msg });
            }
            else
            {
                listBox1.Items.Add(msg);
            }
        }
    }
}