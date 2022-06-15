using System;
using System.Windows.Forms;
using System.Net; //IP Address
using System.Net.Sockets; //Tcp Listener(server, client) class
using System.Threading; //스레드 사용
using System.IO; //Stream 사용
using Microsoft.Win32; //레지스트리 클래스 사용

namespace Messanger
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private TcpListener Server; //Tcp 통신시 클라이언트의 연결을 수신
        private TcpClient SerClient, client; //서버측 클라, 서버에 접속하려는 클라
        private NetworkStream myStream; //네트워크 스트림(데이터 전송 및 수신 통로)
        private StreamReader myRead; //읽기 스트림
        private StreamWriter myWrite; //쓰기 스트림

        private Boolean Start = false; //서버 기동
        private Boolean ClientCon = false; //클라이언트 기동

        private int myPort; //포트번호
        private string myName; //별칭
        private Thread myReader; //전송된 데이터의 읽기 스레드
        private Thread myServer; //서버에 접속하려는 클라이언트를 받아줌

        private RegistryKey key = Registry.LocalMachine.OpenSubKey(
            "SOFTWARE\\Microsoft\\.NETFramework", true);
        
        //전송된 데이터를 출력하는 대리자 작성
        private delegate void AddTextDelegate(string strText);
        private AddTextDelegate AddText = null;

        private void Form1_Load(object sender, EventArgs e)
        {
            //레지스트리에 별칭이 등록되었는지 아닌지 확인
            if((string)key.GetValue("Message_name") == "") //등록되지 않은 경우
            {
                this.myName = this.txtId.Text;
                this.myPort = 62000;
            }

            else //등록된 경우
            {
                try
                {
                    this.myName = (string)key.GetValue("Message_name");
                    this.myPort = 62000;
                }
                catch
                {
                    //Pass
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                ServerStop();
                Disconnection();
            }
            catch
            {
                return;
            }
            
        }

        private void 설정ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.설정ToolStripMenuItem.Enabled = false;
            this.plOption.Visible = true;
            this.txtId.Focus();
            this.txtId.Text = (string)key.GetValue("Message_name"); //별칭
            this.txtPort.Text = "62001"; //포트번호
        }

        private void 닫기ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void MessageView(string strText)
        {
            //메세지 추가
            this.rtbText.AppendText(strText + "\r\n");
            this.rtbText.Focus();
            //마지막 위치로 이동
            this.rtbText.ScrollToCaret();
            this.txtMessage.Focus();
        }

        private void tsbtnConn_Click(object sender, EventArgs e)
        {
            AddText = new AddTextDelegate(MessageView);

            //서버용 기동인지 클라이언트 기동인지 구분
            if (this.cbServer.Checked == true) //서버로 기동
            {
                var addr = new IPAddress(0); //자기 IP사용

                try
                {
                    this.myName = (string)key.GetValue("Message_name");
                }
                catch
                {
                    this.myName = this.txtId.Text;
                    this.myPort = Convert.ToInt32(this.txtPort.Text);
                }

                //서버가 기동 상태가 아니면 서버 기동
                if (!(this.Start))
                {
                    try
                    {
                        Server = new TcpListener(addr, this.myPort);
                        Server.Start();

                        //서버 상태 flag를 기동 상태로 변경
                        this.Start = true;

                        //화면 컨트롤 제어
                        this.txtMessage.Enabled = true;
                        this.btnSend.Enabled = true;
                        this.txtMessage.Focus();
                        this.tsbtnDisconn.Enabled = true;
                        this.tsbtnConn.Enabled = false;
                        this.cbServer.Enabled = false;

                        //서버에 접속하는 클라이언트를 대기 => 스레드
                        myServer = new Thread(ServerStart);
                        myServer.Start();

                        this.설정ToolStripMenuItem.Enabled = false;
                    }
                    catch
                    {
                        //접속 에러 Message로 처리
                        Invoke(AddText, "서버를 실행할 수 없습니다.");
                    }
                } //end of inner if
                else
                {
                    //서버 종료처리
                }

            } //end of outer if
            else //클라이언트로 기동
            {
                if(!(this.ClientCon)) //클라이언트가 기동이 되지 않았는지 => 기동시킴
                {
                    //클라이언트 접속 메소드 호출
                    ClientConnection();
                }
                else
                {
                    this.txtMessage.Enabled = false;
                    this.btnSend.Enabled = false;
                }
            }
        }

        private void ClientConnection()
        {
            try
            {
                //클라이언트가 서버에 접속
                client = new TcpClient(this.txtIp.Text, this.myPort);

                Invoke(AddText, "서버에 접속했습니다.");
                
                //서버와 연결된 통신 통로(스트림) 획득
                myStream = client.GetStream();
                myRead = new StreamReader(myStream);
                myWrite = new StreamWriter(myStream);

                //컨트롤 제어
                this.ClientCon = true;
                this.tsbtnConn.Enabled = false;
                this.tsbtnDisconn.Enabled = true;
                this.txtMessage.Enabled = true;
                this.btnSend.Enabled = true;
                this.txtMessage.Focus();

                //수신 스레드 생성
                //Receive 메소드는 서버, 클라이언트 겸용.
                myReader = new Thread(Receive);
                myReader.Start();

            }
            catch
            {
                //서버에 접속하지 못한 상태를 상태 flag에 반영 후 메세지로 처리
                this.ClientCon = false;
                Invoke(AddText, "서버에 접속에 실패했습니다.");
            }
        }

        private void ServerStop() //서버 종료 및 자원 해제 처리 메소드
        {
            this.Start = false;

            //컨트롤 제어
            this.txtMessage.Enabled = false;
            this.txtMessage.Clear();
            this.btnSend.Enabled = false;
            this.tsbtnConn.Enabled = true;
            this.tsbtnDisconn.Enabled = false;
            this.cbServer.Enabled = true;

            //클라이언트의 접속 상태 flag도 false로 변경
            this.ClientCon = false;

            //**각종 자원 해제**
            //StreamReader 해제
            if (!(myRead == null))
                myRead.Close();
            //StreamWriter 해제
            if (!(myWrite == null))
                myWrite.Close();
            //Networ stream 해제
            if (!(myStream == null))
                myStream.Close();
            //TcpClient(==서버클라이언트) 해제
            if (!(SerClient == null))
                SerClient.Close();
            //TcpListener(==서버) 해제
            if (!(Server == null))
                Server.Stop();
            //수신 스레드 해제
            //클라이언트 접속 담당 스레드 해제
            if (!(myReader == null))
                myReader.Join();

            //서버와 종료된 상황을 메세지로 알림
            if (!(AddText == null))
                Invoke(AddText, "서버와의 접속이 끊어졌습니다.");
        }

        private void Disconnection() //클라이언트 접속 해제 처리 메소드
        {
            this.ClientCon = false;

            try
            {
                //스트림 리더 클래스 해제
                if (!(myRead == null)) 
                    myRead.Close();
                //스트림 쓰는 클래스 헤제
                if (!(myWrite == null))
                    myWrite.Close();
                //네트워크 스트림 해제
                if (!(myStream == null))
                    myStream.Close();
                //TcpClient 해제
                if (!(client == null))
                    client.Close();
                //데이터 수신 스레드 종료
                if (!(myReader == null))
                    myReader.Join();
            }
            catch
            {
                return;
            }
        }

        private void Msg_send() //서버 및 클라이언트가 상대방에게 보내는 메세지를 전송함
        {
            try
            {
                //전송 시간 정보 획득
                var dt = Convert.ToString(DateTime.Now); 
               
                //스트림을 통해 메세지 전송
                myWrite.WriteLine(this.myName + " & " + this.txtMessage + " & " + dt);
                myWrite.Flush();

                //리치텍스트박스에 전송 메세지 추가
                MessageView(this.myName + " : " + this.txtMessage.Text);
                this.txtMessage.Clear();

            }
            catch
            {
                Invoke(AddText, "데이터 전송 중 문제가 발생했습니다.");
                this.txtMessage.Clear();
            }
        }

        private void ServerStart()
        {
            Invoke(AddText, "서버 실행 : 클라이언트의 접속을 기다립니다...");

            while (Start)
            {
                try
                {
                    //서버에 접속된 클라이언트
                    SerClient = Server.AcceptTcpClient();
                    Invoke(AddText, "클라이언트가 접속되었습니다.");

                    myStream = SerClient.GetStream();

                    myRead = new StreamReader(myStream);
                    myWrite = new StreamWriter(myStream);

                    this.ClientCon = true;

                    //클라이언트가 보내온 데이터를 수신하는 스레드
                    myReader = new Thread(Receive);
                    myReader.Start();
                }
                catch
                {

                }
            }
        }

        private void Receive()
        {
            try
            {
                while (this.ClientCon) //클라이언트가 접속되어있는 동안
                {
                    Thread.Sleep(1);

                    //스트림으로부터 읽을 게 있는가?
                    if (myStream.CanRead) //있는 경우
                    {
                        var msg = myRead.ReadLine(); //읽어라
                        var Smsg = msg.Split('&');

                        if (Smsg[0] == "S001")
                        {
                            //전송 시각 처리 but 생략
                        }
                        else
                        {
                            if (msg.Length > 0)
                                Invoke(AddText, Smsg[0] + " : " + Smsg[1]); 
                                //서버 및 클라이언트가 보내온 메세지 출력
                        }

                        //수신 데이터 처리
                    }
                }
            }
            catch
            {
                //Pass
            }
        }

        private void tsbtnDisconn_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.cbServer.Checked) //클라로 기동? 서버로 기동?
                {
                    if (this.SerClient.Connected) //서버로 기동 
                    {
                        var dt = Convert.ToString(DateTime.Now);
                        myWrite.WriteLine(this.myName + " & " + "채팅 APP이 종료되었습니다." + " & " + dt);
                        myWrite.Flush();
                        // -> 클라에게 서버 연결이 끊겼음을 공지
                    }
                }
                else //클라로 기동
                {
                    if (this.client.Connected)
                    {
                        var dt = Convert.ToString(DateTime.Now);
                        myWrite.WriteLine(this.myName + " & " + "채팅 APP이 종료되었습니다." + " & " + dt);
                        myWrite.Flush();
                        // -> 서버에게 클라 연결이 끊겼음을 공지
                    }
                }
            }
            catch
            {
                //Pass
            }
            ServerStop();
            this.설정ToolStripMenuItem.Enabled = true;
        }

        private void cbServer_CheckedChanged(object sender, EventArgs e)
        {
            if (this.cbServer.Checked)
                this.txtIp.Enabled = false; 
                //서버로 기동시 IP는 내부적으로 사용 => 입력 막음
            else
                this.txtIp.Enabled = true; 
                //클라이언트로 기동시 접속할 IP를 입력받음
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (this.cbServer.Checked == true) //서버용으로 사용
            {
                //입력된 정보를 레지스트리에 등록
                ControlCheck();
            }
            else //클라이언트용으로 사용
            {
                if (this.txtIp.Text == "")
                    this.txtIp.Focus();
                else
                {
                    //입력된 정보를 레지스트리에 등록
                    ControlCheck();
                }
            }
        }

        private void ControlCheck()
        {
            if (this.txtId.Text == "")
                this.txtId.Focus();
            else if (this.txtPort.Text == "")
                this.txtPort.Focus();
            else
            {
                //입력된 정보를 레지스트리에 저장
                try
                {
                    //레지스트리에 별칭 및 포트 저장
                    var name = this.txtId.Text;
                    var port = this.txtPort.Text;
                    key.SetValue("Message_name", name);
                    key.SetValue("Message_port", port);

                    //컨트롤 제어
                    this.plOption.Visible = false;
                    this.설정ToolStripMenuItem.Enabled = true;
                    this.tsbtnConn.Enabled = true;
                }
                catch(Exception ex)
                {
                    MessageBox.Show($"설정 정보가 저장되지 않았습니다. {ex}", "에러",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.설정ToolStripMenuItem.Enabled = true;
            this.plOption.Visible = false;
            this.txtMessage.Focus();
        }

        private void txtMessage_KeyPress(object sender, KeyPressEventArgs e)
        {
            //엔터키가 입력된 경우
            if (e.KeyChar == (char)13)
            {
                if (this.txtMessage.Text == "")
                    this.txtMessage.Focus();
                else
                    Msg_send();
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            Msg_send();
        }
    }
}
