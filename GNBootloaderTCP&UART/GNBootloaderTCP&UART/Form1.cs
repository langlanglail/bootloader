
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Text.Unicode;

namespace GNBootloaderTCP_UART
{
    public partial class Form1 : Form
    {
        //全局变量定义
        #region
        //数据接收长度
        private const int ReceiveDataBufLength = 4096;
        private byte connect_device = 0;//连接外设选择
                                        //1:串口
                                        //2:TCP

        //串口相关
        private static SerialPort serialPort1 = new SerialPort();//串口
        //int SerialRecveLength;//串口数据接收长度
        //byte[] SerialReceiveBuf = new byte[ReceiveDataBufLength];//串口数据缓存长度
        //bool SerialReceiveFlag = false;//串口接收完成

        //TCP客户端
        private static TcpClient tcpClient;  //Tcp客户端模型
        private static NetworkStream networkStream;// 网络访问数据流
                                                   //  public static List<byte[]> ResponseBytes = new List<byte[]>();// 返回数据
                                                   //public static byte[] ResponseBytes = new byte[ReceiveDataBufLength];// 返回数据
        private static string RemoteIp = string.Empty;// 远程服务IP地址
        private static int RemotePort = -1;// 远程服务IP地址对应端口
        private static bool IsConnected = false;// 是否连接

        //串口、TCP客户端
        //数据接收长度、标志位、数组
        private static int TCPSerialRecveLength;//串口数据接收长度
        private static byte[] TCPSerialReceiveBuf = new byte[ReceiveDataBufLength];//串口数据缓存长度
        private static bool TCPSerialReceiveFlag = false;//串口接收完成
        private const int TCPSerialBufferOffset = 5;//接收到数据以后，偏移5个地址后才是有效数据

        //记录部分实时通讯的数据
        //用于解析接收数据时，判断是否超时或续发等功能
        private byte CommunicationAdd = 0;//记录当前指令的地址
        private GNBootLoaderCmd_G CommunicationCmdLast1 = 0;//记录当前指令
        private GNBootLoaderCmd_Fun_G CommunicationCmdLast2 = 0;//记录当前指令数据
        int DataCountDown = 0;//等待接收倒计时
        bool DataCountDownFly = false;//等待接收标志位

        //设备号  表示当前连接的设备
        private byte CommentionAdd = 0;

        //用于打开升级文件使用
        private OpenFileDialog fileDialog = new OpenFileDialog();
        private FileStream fs;
        private byte Data_ok = 0;
        private byte[] Data_bin = new byte[1024 * 1024];
        private long Length_bin = 0;//文件大小
        private bool BinSendFlag = false;//是否开始升级
        byte BinSendStep = 0;//发送步骤
        private long BinSendRemanet = 0;//剩余数据个数
        private int BinSendNumt = 0;//发送计数
        private int BinSendTim = 0;//发送计时
        private int SendTIMM = 1000;//发送间隔时间
        private const int SendTIMM_CONST = 1000;//发送间隔的固定时间

        //线程运行
        //用于处理接收数据
        private Thread thread;//线程


        //默认的设备号
        private byte[] DeviceNum = { 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 };
        #endregion

        public Form1()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 软件初始加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {
            SearchSerialPort();//搜索当前可用串口
            comboBox2.SelectedIndex = 7;//选择115200波特率

            //线程运行
            thread = new Thread(new ThreadStart(DoWork));
            thread.IsBackground = true;
            thread.Start();


            //屏蔽此部分显示，只是留作写程序参考         
            tabPage4.Parent = null;//此部分一直不显示

            //打开串口，选择设备号之后，会使能
            tabPage3.Parent = null;//不显示系统升级
            tabPage5.Parent = null;//不显示17号设备的配置界面
            tabPage6.Parent = null;//不显示18号设备的配置界面

            //设备号选择 不使能
            groupBox1.Enabled = false;


            toolStripStatusLabel2.Alignment = ToolStripItemAlignment.Right;
            toolStripStatusLabel3.Alignment = ToolStripItemAlignment.Right;
            toolStripStatusLabel3.Text = "";

            //擦除APP不可用
            button8.Enabled = false;
        }

        /// <summary>
        /// 线程 用于处理接收数据
        /// </summary>
        private void DoWork()
        {
            CheckForIllegalCrossThreadCalls = false;

            //临时使用
            byte[] strsttt = new byte[100];
            byte[] bindatabuf = new byte[4096];
            while (true)
            {
                //设备在进行传输升级文件，需要连续发送
                if (BinSendFlag == true && Data_ok > 0)
                {
                    switch (BinSendStep)
                    {
                        case 0://启动发送
                            {

                                if (SendTIMM < 60)
                                {//开始启动发送以后
                                 //发送开始下载指令

                                    byte[] databuf = new byte[2];

                                    if (Data_ok == 1)
                                    {//APP升级文件
                                        databuf[0] = 0x01;
                                    }
                                    else if (Data_ok == 2)
                                    {//固件升级文件
                                        databuf[0] = 0x11;
                                    }
                                    SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_Download, databuf, 1);

                                    BinSendRemanet = Length_bin;//剩余总数
                                    BinSendNumt = 0;//发送计数


                                    BinSendTim = 0;//重新开始计时
                                    SendTIMM = SendTIMM_CONST;//重新开始倒计时1秒

                                    BinSendStep++;
                                }
                            }
                            break;
                        case 2://发送升级文件
                            {
                                if ((DataCountDown < 201) && (DataCountDown > 100))
                                {//接收到数据


                                    if (Data_ok == 1)
                                    {//APP升级文件
                                        bindatabuf[0] = 0x02;//正在下载
                                    }
                                    else if (Data_ok == 2)
                                    {//固件升级文件
                                        bindatabuf[0] = 0x12;//正在下载
                                    }

                                    if (BinSendRemanet > 512)
                                    {//剩余字节大于512个字节

                                        //每次读取512个字节
                                        for (int i = 0; i < 512; i++)
                                        {
                                            bindatabuf[1 + i] = Data_bin[BinSendNumt];
                                            BinSendRemanet--;
                                            BinSendNumt++;
                                        }

                                        SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_Download, bindatabuf, 513);

                                    }
                                    else
                                    {
                                        //读取剩余字节个数
                                        int datttnumlen = (int)BinSendRemanet;
                                        for (int i = 0; i < datttnumlen; i++)
                                        {
                                            bindatabuf[1 + i] = Data_bin[BinSendNumt];
                                            BinSendRemanet--;
                                            BinSendNumt++;
                                        }

                                        SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_Download, bindatabuf, datttnumlen + 1);

                                        BinSendStep = 3;
                                    }

                                    toolStripProgressBar1.Value = (int)(100 * (Length_bin - BinSendRemanet) / Length_bin);
                                }
                            }
                            break;
                        case 3:
                            if ((DataCountDown < 201) && (DataCountDown > 100))
                            {
                                byte[] databuf1 = new byte[2];
                                if (Data_ok == 1)
                                {//APP升级文件
                                    databuf1[0] = 0x03;
                                }
                                else if (Data_ok == 2)
                                {//固件升级文件
                                    databuf1[0] = 0x13;
                                }

                                SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_Download, databuf1, 1);

                                BinSendFlag = false;

                                textBox1.AppendText("总时间" + BinSendTim.ToString() + "\r\n");//对话框追加显示数据

                                //关闭文件
                                Array.Clear(Data_bin, 0, Data_bin.Length);//先清空上次的缓存内容
                                fs.Close();
                                toolStripStatusLabel3.Text = "文件下载完成";
                                toolStripProgressBar1.Value = 0;

                                Data_ok = 0;
                            }
                            break;
                    }
                }

                //超时检测
                //数据反馈超时  判断异常
                if (DataCountDownFly == true)
                {
                    if (DataCountDown < 51)
                    {
                        DataCountDownFly = false;
                        textBox1.AppendText("设备超时\r\n");//对话框追加显示数据
                        //超时后，升级文件不再发送
                        if (BinSendFlag == true)
                        {
                            BinSendFlag = false;
                        }
                    }
                    Thread.Sleep(5);//延时5ms 等待接收完数据
                }
                else
                {
                    Thread.Sleep(2);//延时2ms 等待接收完数据
                }

                //等待接收数据  同时接收到了数据
                if (TCPSerialReceiveFlag == true)
                {
                    TCPSerialReceiveFlag = false;//清掉数据接收标志位

                    if (CommunicationAdd == TCPSerialReceiveBuf[0])
                    { //地址判断
                        if (checksum(TCPSerialReceiveBuf, TCPSerialRecveLength - 1) == TCPSerialReceiveBuf[TCPSerialRecveLength - 1])
                        { //校验位检查

                            TCPSerialReceiveBuf[1] -= 0x80;
                            if ((byte)CommunicationCmdLast2 == TCPSerialReceiveBuf[2] && ((byte)(CommunicationCmdLast1) == TCPSerialReceiveBuf[1]))
                            { //指令和指令数据检查
                              //返回数据应该与发送的指令是一致的

                                //有效的数据返回
                                DataCountDownFly = false;
                                //返回数据的长度
                                int datlen = (int)((TCPSerialReceiveBuf[3] << 8) + TCPSerialReceiveBuf[4]);

                                switch ((GNBootLoaderCmd_G)TCPSerialReceiveBuf[1])
                                {
                                    case GNBootLoaderCmd_G.BOOT_CDM_WRITE://0x01  升级相关的写指令
                                        {
                                            switch ((GNBootLoaderCmd_Fun_G)TCPSerialReceiveBuf[2])
                                            {
                                                case GNBootLoaderCmd_Fun_G.BOOT_WRITE_Rst://复位

                                                    if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x01)
                                                    {
                                                        textBox1.AppendText("设备复位\r\n");//对话框追加显示数据
                                                    }
                                                   else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x02)
                                                    {
                                                        textBox1.AppendText("设备复位失败\r\n");//对话框追加显示数据
                                                    }
                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_WRITE_EraseChip://擦除

                                                    if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x01)
                                                    {
                                                        textBox1.AppendText("开始擦除BAK区\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x02)
                                                    {
                                                        textBox1.AppendText("擦除BAK区完成\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x03)
                                                    {
                                                        textBox1.AppendText("开始擦除APP区\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x04)
                                                    {
                                                        textBox1.AppendText("擦除APP区完成\r\n");//对话框追加显示数据
                                                    }
                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_WRITE_Jump_Back://跳转

                                                    if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x01)
                                                    {
                                                        textBox1.AppendText("跳转至APP\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x02)
                                                    {
                                                        textBox1.AppendText("跳转至APP失败\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x03)
                                                    {
                                                        textBox1.AppendText("跳转至BOOT\r\n");//对话框追加显示数据                                                  
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x04)
                                                    {
                                                        textBox1.AppendText("跳转至BOOT失败\r\n");//对话框追加显示数据
                                                    }
                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_WRITE_Download://下载

                                                    if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x01)
                                                    {
                                                        if (BinSendStep == 1)
                                                        {
                                                            BinSendStep++;
                                                            DataCountDownFly = true;

                                                            DataCountDown = 249;//接收完数据后  50ms发送下一包数据

                                                        }
                                                        textBox1.AppendText("开始下载APP文件\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x02)
                                                    {
                                                        DataCountDownFly = true;

                                                        DataCountDown = 249;//接收完数据后  50ms发送下一包数据

                                                        //       textBox1.AppendText("正在下载\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x03)
                                                    {
                                                        textBox1.AppendText("APP结束下载\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x11)
                                                    {
                                                        if (BinSendStep == 1)
                                                        {
                                                            BinSendStep++;
                                                            DataCountDownFly = true;

                                                            DataCountDown = 249;//接收完数据后  50ms发送下一包数据

                                                        }
                                                        textBox1.AppendText("开始下载固件文件\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x12)
                                                    {
                                                        DataCountDownFly = true;

                                                        DataCountDown = 249;//接收完数据后  50ms发送下一包数据

                                                        //       textBox1.AppendText("正在下载\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x13)
                                                    {
                                                        textBox1.AppendText("固件结束下载\r\n");//对话框追加显示数据
                                                    }

                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_WRITE_CheckComp://校验程序

                                                    if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x01)
                                                    {
                                                        textBox1.AppendText("开始校验BAK\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 6 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x02)
                                                    {
                                                        byte bakcrc = 0xA0;
                                                        int bak_len = 0;
                                                        if (Length_bin > 5 * 1024)
                                                        {
                                                            for (int loc = 0; loc < ((int)Length_bin) / 2048 * 2048; loc++)
                                                            {
                                                                bakcrc += Data_bin[loc];
                                                                bak_len++;
                                                            }
                                                        }
                                                        //textBox1.AppendText("bakcrc    " + bakcrc.ToString("X2") + "\r\n");//对话框追加显示数据
                                                        //textBox1.AppendText("bak_len   " + bak_len.ToString() + "\r\n");//对话框追加显示数据

                                                        int reclen = 0;

                                                        reclen = TCPSerialReceiveBuf[TCPSerialBufferOffset + 5];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 4];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 3];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 2];

                                                        //textBox1.AppendText("TCPSerialReceiveBuf[TCPSerialBufferOffset + 1]     " + TCPSerialReceiveBuf[TCPSerialBufferOffset + 1].ToString("X2") + "\r\n");//对话框追加显示数据
                                                        //textBox1.AppendText("reclen     " + reclen.ToString() + "\r\n");//对话框追加显示数据

                                                        if ((bakcrc == TCPSerialReceiveBuf[TCPSerialBufferOffset + 1])
                                                            && (bak_len == reclen)

                                                            )
                                                        {
                                                            textBox1.AppendText("BAK校验完成:BAK与打开的文件一致\r\n");//对话框追加显示数据
                                                        }
                                                        else
                                                        {
                                                            textBox1.AppendText("BAK校验完成:BAK与打开的文件有差异\r\n");//对话框追加显示数据
                                                        }
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x03)
                                                    {
                                                        textBox1.AppendText("开始校验APP\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 6 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x04)
                                                    {
                                                        byte bakcrc = 0xA0;
                                                        int bak_len = 0;
                                                        if (Length_bin > 5 * 1024)
                                                        {


                                                            for (int loc = 0; loc < ((int)Length_bin) / 2048 * 2048; loc++)
                                                            {
                                                                bakcrc += Data_bin[loc];
                                                                bak_len++;
                                                            }
                                                        }


                                                        int reclen = 0;

                                                        reclen = TCPSerialReceiveBuf[TCPSerialBufferOffset + 5];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 4];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 3];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 2];



                                                        if ((bakcrc == TCPSerialReceiveBuf[TCPSerialBufferOffset + 1])
                                                            && (bak_len == reclen)

                                                            )
                                                        {
                                                            textBox1.AppendText("APP校验完成:APP与打开的文件一致\r\n");//对话框追加显示数据
                                                        }
                                                        else
                                                        {
                                                            textBox1.AppendText("APP校验完成:APP与打开的文件有差异\r\n");//对话框追加显示数据
                                                        }
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x05)
                                                    {
                                                        textBox1.AppendText("开始校验BAK与APP\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 2 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x06)
                                                    {
                                                        if (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] == 0)
                                                        {
                                                            textBox1.AppendText("BAK与APP校验完成:BAK与APP一致\r\n");//对话框追加显示数据
                                                        }
                                                        else
                                                        {
                                                            textBox1.AppendText("BAK与APP校验完成:BAK与APP有差异\r\n");//对话框追加显示数据
                                                        }
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x11)
                                                    {
                                                        textBox1.AppendText("开始校验Firmware\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 6 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x12)
                                                    {
                                                        byte bakcrc = 0xA0;
                                                        int bak_len = 0;
                                                        if (Length_bin < 128 * 1024)
                                                        {
                                                            if (Length_bin < 2048)
                                                            {
                                                                for (int loc = 0; loc < 2048; loc++)
                                                                {
                                                                    if (loc > Length_bin)
                                                                    {
                                                                        Data_bin[loc] = 0xff;
                                                                    }
                                                                    bakcrc += Data_bin[loc];
                                                                    bak_len++;
                                                                }
                                                            }
                                                            else
                                                            {
                                                                for (int loc = 0; loc < ((int)Length_bin) / 2048 * 2048; loc++)
                                                                {
                                                                    bakcrc += Data_bin[loc];
                                                                    bak_len++;
                                                                }

                                                            }
                                                        }
                                                        //textBox1.AppendText("bakcrc    " + bakcrc.ToString("X2") + "\r\n");//对话框追加显示数据
                                                        //textBox1.AppendText("bak_len   " + bak_len.ToString() + "\r\n");//对话框追加显示数据

                                                        int reclen = 0;

                                                        reclen = TCPSerialReceiveBuf[TCPSerialBufferOffset + 5];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 4];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 3];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 2];

                                                        //textBox1.AppendText("TCPSerialReceiveBuf[TCPSerialBufferOffset + 1]     " + TCPSerialReceiveBuf[TCPSerialBufferOffset + 1].ToString("X2") + "\r\n");//对话框追加显示数据
                                                        //textBox1.AppendText("reclen     " + reclen.ToString() + "\r\n");//对话框追加显示数据

                                                        if ((bakcrc == TCPSerialReceiveBuf[TCPSerialBufferOffset + 1])
                                                            && (bak_len == reclen)

                                                            )
                                                        {
                                                            textBox1.AppendText("Firmware校验完成:Firmware与打开的文件一致\r\n");//对话框追加显示数据
                                                        }
                                                        else
                                                        {
                                                            textBox1.AppendText("Firmware校验完成:Firmware与打开的文件有差异\r\n");//对话框追加显示数据
                                                        }
                                                    }

                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_WRITE_StartLoad://BAK加载

                                                    if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x01)
                                                    {
                                                        textBox1.AppendText("开始擦除APP区\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x02)
                                                    {
                                                        textBox1.AppendText("擦除APP区完成\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x03)
                                                    {
                                                        textBox1.AppendText("开始加载BAK区\r\n");//对话框追加显示数据
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x04)
                                                    {
                                                        textBox1.AppendText("加载BAK区完成\r\n");//对话框追加显示数据
                                                    }
                                                    break;

                                            }
                                        }
                                        break;
                                    case GNBootLoaderCmd_G.BOOT_CDM_READ://0x02  升级相关的读指令
                                        {
                                            Array.Clear(strsttt, 0, strsttt.Length);
                                            switch ((GNBootLoaderCmd_Fun_G)TCPSerialReceiveBuf[2])
                                            {
                                                case GNBootLoaderCmd_Fun_G.BOOT_READ_Runstatus://读取运行状态

                                                    for (int loc = 0; loc < datlen; loc++)
                                                    {
                                                        strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                    }

                                                    textBox1.AppendText(System.Text.Encoding.Default.GetString(strsttt));//对话框追加显示数据       
                                                    textBox1.AppendText("\r\n");
                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_READ_SoftVerion://读取软件版本号

                                                    for (int loc = 0; loc < datlen; loc++)
                                                    {
                                                        strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                    }

                                                    textBox1.AppendText(System.Text.Encoding.Default.GetString(strsttt));//对话框追加显示数据       
                                                    textBox1.AppendText("\r\n");
                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_READ_HardVerion://读取硬件版本号

                                                    for (int loc = 0; loc < datlen; loc++)
                                                    {
                                                        strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                    }

                                                    textBox1.AppendText(System.Text.Encoding.Default.GetString(strsttt));//对话框追加显示数据       
                                                    textBox1.AppendText("\r\n");
                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_READ_FirmwareVerion://读取固件版本号

                                                    for (int loc = 0; loc < datlen; loc++)
                                                    {
                                                        strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                    }

                                                    textBox1.AppendText(System.Text.Encoding.Default.GetString(strsttt));//对话框追加显示数据       
                                                    textBox1.AppendText("\r\n");
                                                    break;

                                                case GNBootLoaderCmd_Fun_G.BOOT_READ_DeviceDes://读取设备描述

                                                    for (int loc = 0; loc < datlen; loc++)
                                                    {
                                                        strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                    }

                                                    textBox1.AppendText(Encoding.Default.GetString(strsttt));//对话框追加显示数据       
                                                    textBox1.AppendText("\r\n");
                                                    break;

                                            }
                                        }
                                        break;
                                    case GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE://0x03  系统参数写入
                                    case GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ://0x04  系统参数读取
                                        {
                                            switch (TCPSerialReceiveBuf[0])
                                            {//根据当前设备号，显示在不同的区域
                                                case 1:
                                                    {
                                                        switch ((GNBootLoaderCmd_Fun_G)TCPSerialReceiveBuf[2])
                                                        {
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U8_WRITE_READ1://U8  
                                                                {
                                                                    textBox5.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox5.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U16_WRITE_READ1://U16
                                                                {
                                                                    UInt16 add = (UInt16)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8));


                                                                    textBox6.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox6.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U32_WRITE_READ1://U32
                                                                {
                                                                    UInt32 add = (UInt32)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8) + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 2] << 16) + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 3] << 24));


                                                                    textBox7.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox7.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF20_WRITE_READ1://buf[20]  序列号
                                                                Array.Clear(strsttt, 0, strsttt.Length);
                                                                for (int loc = 0; loc < datlen; loc++)
                                                                {
                                                                    strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                                }
                                                                textBox2.Text = System.Text.Encoding.Default.GetString(strsttt);

                                                                textBox1.AppendText(textBox2.Text);//对话框追加显示数据       
                                                                textBox1.AppendText("\r\n");

                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF40_WRITE_READ1://buf[40]   烧录日期
                                                                Array.Clear(strsttt, 0, strsttt.Length);
                                                                for (int loc = 0; loc < datlen; loc++)
                                                                {
                                                                    strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                                }

                                                                textBox1.AppendText(System.Text.Encoding.Default.GetString(strsttt));//对话框追加显示数据       
                                                                textBox1.AppendText("\r\n");
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF100_WRITE_READ1://buf[100]
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_IP_WRITE_READ://IP地址
                                                                {
                                                                    textBox9.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 1].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 2].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 3].ToString();

                                                                    textBox1.AppendText("IP地址:");//对话框追加显示数据 
                                                                    textBox1.AppendText(textBox9.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_MAC_WRITE_READ://MAC地址
                                                                {
                                                                    textBox8.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 1].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 2].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 3].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 4].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 5].ToString();

                                                                    textBox1.AppendText("MAC地址:");//对话框追加显示数据 
                                                                    textBox1.AppendText(textBox8.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_USART_BUND_WRITE_READ://串口波特率
                                                                {
                                                                    comboBox3.SelectedIndex = TCPSerialReceiveBuf[TCPSerialBufferOffset];


                                                                    textBox1.AppendText("串口波特率:");//对话框追加显示数据     
                                                                    textBox1.AppendText(comboBox3.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_DEVICE_ADD_WRITE_READ://串口波特率
                                                                {
                                                                    numericUpDown3.Value = TCPSerialReceiveBuf[TCPSerialBufferOffset];


                                                                    textBox1.AppendText("设备号:");//对话框追加显示数据     
                                                                    textBox1.AppendText(numericUpDown3.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                        }
                                                    }
                                                    break;
                                                case 17:
                                                    {
                                                        switch ((GNBootLoaderCmd_Fun_G)TCPSerialReceiveBuf[2])
                                                        {
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U8_WRITE_READ1://U8  
                                                                {
                                                                    textBox15.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox15.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U16_WRITE_READ1://U16
                                                                {
                                                                    UInt16 add = (UInt16)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8));


                                                                    textBox14.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox14.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U32_WRITE_READ1://U32
                                                                {
                                                                    UInt32 add = (UInt32)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8) + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 2] << 16) + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 3] << 24));


                                                                    textBox13.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox13.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF20_WRITE_READ1://buf[20]  序列号
                                                                Array.Clear(strsttt, 0, strsttt.Length);
                                                                for (int loc = 0; loc < datlen; loc++)
                                                                {
                                                                    strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                                }
                                                                textBox10.Text = Encoding.Default.GetString(strsttt);
                                                                //   textBox10.Text = Encoding.GetEncoding("GB2312").GetString(strsttt);


                                                                textBox1.AppendText(textBox10.Text);//对话框追加显示数据       
                                                                textBox1.AppendText("\r\n");

                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF40_WRITE_READ1://buf[40]   烧录日期
                                                                Array.Clear(strsttt, 0, strsttt.Length);
                                                                for (int loc = 0; loc < datlen; loc++)
                                                                {
                                                                    strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                                }

                                                                textBox1.AppendText(Encoding.Default.GetString(strsttt));//对话框追加显示数据       
                                                                textBox1.AppendText("\r\n");
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF100_WRITE_READ1://buf[100]
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_IP_WRITE_READ://IP地址
                                                                {
                                                                    textBox12.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 1].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 2].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 3].ToString();

                                                                    textBox1.AppendText("IP地址:");//对话框追加显示数据 
                                                                    textBox1.AppendText(textBox12.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_MAC_WRITE_READ://MAC地址
                                                                {
                                                                    textBox11.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 1].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 2].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 3].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 4].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 5].ToString();

                                                                    textBox1.AppendText("MAC地址:");//对话框追加显示数据 
                                                                    textBox1.AppendText(textBox11.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                        }
                                                    }
                                                    break;
                                                case 18:
                                                    {
                                                        switch ((GNBootLoaderCmd_Fun_G)TCPSerialReceiveBuf[2])
                                                        {
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U8_WRITE_READ1://U8  
                                                                {
                                                                    textBox20.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox20.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U16_WRITE_READ1://U16
                                                                {
                                                                    UInt16 add = (UInt16)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8));


                                                                    textBox19.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox19.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U32_WRITE_READ1://U32
                                                                {
                                                                    UInt32 add = (UInt32)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8) + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 2] << 16) + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 3] << 24));


                                                                    textBox18.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox18.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF20_WRITE_READ1://buf[20]  序列号
                                                                Array.Clear(strsttt, 0, strsttt.Length);
                                                                for (int loc = 0; loc < datlen; loc++)
                                                                {
                                                                    strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                                }
                                                                textBox21.Text = Encoding.Default.GetString(strsttt);
                                                                //   textBox10.Text = Encoding.GetEncoding("GB2312").GetString(strsttt);


                                                                textBox1.AppendText(textBox21.Text);//对话框追加显示数据       
                                                                textBox1.AppendText("\r\n");

                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF40_WRITE_READ1://buf[40]   烧录日期

                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF100_WRITE_READ1://buf[100]
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_IP_WRITE_READ://IP地址

                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_MAC_WRITE_READ://MAC地址

                                                                break;
                                                        }
                                                    }
                                                    break;

                                            }
                                        }
                                        break;
                                    case GNBootLoaderCmd_G.BOOT_SELFCONFIG_WRITE://0x05  系统参数写入
                                    case GNBootLoaderCmd_G.BOOT_SELFCONFIG_READ://0x06  系统参数读取
                                        {
                                            switch (TCPSerialReceiveBuf[0])
                                            {//根据当前设备号，显示在不同的区域
                                                case 1:
                                                    {
                                                        switch (TCPSerialReceiveBuf[2])
                                                        {
                                                            case 1://version 
                                                                {
                                                                    textBox17.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox17.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case 2://version2
                                                                {
                                                                    UInt16 add = (UInt16)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8));


                                                                    textBox16.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox16.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                        }
                                                    }
                                                    break;
                                                case 17:
                                                    {
                                                        switch (TCPSerialReceiveBuf[2])
                                                        {
                                                            case 1://version 
                                                                {
                                                                    textBox17.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox17.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case 2://version2
                                                                {
                                                                    UInt16 add = (UInt16)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8));


                                                                    textBox16.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox16.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                        }
                                                    }
                                                    break;
                                                case 18:
                                                    {

                                                        switch (TCPSerialReceiveBuf[2])
                                                        {
                                                            case 1://version 
                                                                {
                                                                    textBox23.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox23.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case 2://version2
                                                                {
                                                                    UInt16 add = (UInt16)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8));


                                                                    textBox22.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox22.Text);//对话框追加显示数据       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                        }
                                                    }
                                                    break;
                                            }
                                            break;
                                        }

                                }

                            }
                        }
                    }
                }

            }
        }



        //数据升级指令发送
        #region CMD 通用指令和特殊指令的示例


        //系统参数读取  03 04指令
        #region CDM03&CMD04

        /// <summary>
        /// 1# U8参数写入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button49_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = Convert.ToByte(textBox5.Text);

            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_U8_WRITE_READ1, databuf, 1);
        }
        /// <summary>
        /// 1# U8参数读取
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button48_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_U8_WRITE_READ1, databuf, 0);
        }
        /// <summary>
        ///1#  U16参数写入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button19_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            UInt16 addd = Convert.ToUInt16(textBox6.Text);

            databuf[0] = (byte)(addd);
            databuf[1] = (byte)(addd >> 8);
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_U16_WRITE_READ1, databuf, 2);
        }
        /// <summary>
        ///1#  U16参数读取
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_U16_WRITE_READ1, databuf, 0);
        }
        /// <summary>
        ///1#  U32参数写入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button21_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[4];
            UInt32 addd = Convert.ToUInt32(textBox7.Text);
            databuf[3] = (byte)(addd >> 24);
            databuf[2] = (byte)(addd >> 16);
            databuf[1] = (byte)(addd >> 8);
            databuf[0] = (byte)(addd);
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_U32_WRITE_READ1, databuf, 4);

        }
        /// <summary>
        ///1#  U32参数读取
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button20_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_U32_WRITE_READ1, databuf, 0);
        }


        /// <summary>
        ///1#  写IP地址
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button25_Click(object sender, EventArgs e)
        {
            try
            {
                string[] words = textBox9.Text.Split('.');
                byte[] databuf = new byte[4];

                databuf[0] = Convert.ToByte(words[0]);
                databuf[1] = Convert.ToByte(words[1]);
                databuf[2] = Convert.ToByte(words[2]);
                databuf[3] = Convert.ToByte(words[3]);
                SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_IP_WRITE_READ, databuf, 4);
            }
            catch
            { }

        }

        /// <summary>
        ///1#  读取IP地址
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button22_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_IP_WRITE_READ, databuf, 0);
        }
        /// <summary>
        ///1#  写MAC地址
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button24_Click(object sender, EventArgs e)
        {
            try
            {
                string[] words = textBox8.Text.Split('.');
                byte[] databuf = new byte[6];


                databuf[0] = Convert.ToByte(words[0]);
                databuf[1] = Convert.ToByte(words[1]);
                databuf[2] = Convert.ToByte(words[2]);
                databuf[3] = Convert.ToByte(words[3]);
                databuf[4] = Convert.ToByte(words[4]);
                databuf[5] = Convert.ToByte(words[5]);
                SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_MAC_WRITE_READ, databuf, 6);
            }
            catch
            { }
        }
        /// <summary>
        /// 1#   读取MAC地址
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button23_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_MAC_WRITE_READ, databuf, 0);
        }


        /// <summary>
        ///1#  波特率写入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button27_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = (byte)comboBox3.SelectedIndex;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_USART_BUND_WRITE_READ, databuf, 1);
        }
        /// <summary>
        ///1#  波特率读取
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button26_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_USART_BUND_WRITE_READ, databuf, 0);
        }
        /// <summary>
        ///1#  设备号写入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button35_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = Convert.ToByte(numericUpDown3.Text);

            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_DEVICE_ADD_WRITE_READ, databuf, 1);
        }
        /// <summary>
        ///1#  设备号读取
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button28_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_DEVICE_ADD_WRITE_READ, databuf, 0);
        }

        /// <summary>
        ///1#  写入烧录日期
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button14_Click(object sender, EventArgs e)
        {
            DateTime dt = DateTime.Now;
            byte[] databuf = System.Text.Encoding.Default.GetBytes(dt.ToString());
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF40_WRITE_READ1, databuf, databuf.Length);

        }
        /// <summary>
        ///1#  写入设备序列号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button42_Click(object sender, EventArgs e)
        {
            if ((textBox2.TextLength < 20) && (textBox2.TextLength > 5))
            {
                SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF20_WRITE_READ1, System.Text.Encoding.Default.GetBytes(textBox2.Text), textBox2.TextLength);
            }
            else
            {
                MessageBox.Show("字符长度不合适,宽度为5-20个字符长度!", "警告");
            }
        }

        #endregion
        //0x02 指令读取
        #region CDM02

        /// <summary>
        /// 读取运行状态
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button15_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_READ, GNBootLoaderCmd_Fun_G.BOOT_READ_Runstatus, databuf, 0);
        }
        /// <summary>
        /// 读取软件版本号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_READ, GNBootLoaderCmd_Fun_G.BOOT_READ_SoftVerion, databuf, 0);
        }
        /// <summary>
        /// 读取硬件版本号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button17_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_READ, GNBootLoaderCmd_Fun_G.BOOT_READ_HardVerion, databuf, 0);
        }
        /// <summary>
        /// 读取固件版本号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button29_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_READ, GNBootLoaderCmd_Fun_G.BOOT_READ_FirmwareVerion, databuf, 0);
        }
        /// <summary>
        ///1#  读取烧录日期
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button18_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF40_WRITE_READ1, databuf, 0);
        }
        /// <summary>
        /// 读取设备描述符
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button41_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_READ, GNBootLoaderCmd_Fun_G.BOOT_READ_DeviceDes, databuf, 0);
        }
        /// <summary>
        ///1#  读取设备序列号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button43_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF20_WRITE_READ1, databuf, 0);
        }

        #endregion  CMD
        //0x01 指令发送
        #region  CMD01


        /// <summary>
        /// 设备重启
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button9_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_Rst, databuf, 1);
        }

        /// <summary>
        /// 擦除BAK
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button4_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_EraseChip, databuf, 1);
        }
        /// <summary>
        /// 擦除APP
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button8_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x02;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_EraseChip, databuf, 1);
        }

        /// <summary>
        /// 校验Firmware 与打开文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button31_Click(object sender, EventArgs e)
        {
            if (Data_ok == 2)
            {
                byte[] databuf = new byte[2];
                databuf[0] = 0x11;
                SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_CheckComp, databuf, 1);
            }
            else
            {
                textBox1.AppendText("请打开Firmware文件\r\n");//对话框追加显示数据
            }

        }
        /// <summary>
        /// 校验BAK与打开文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button40_Click(object sender, EventArgs e)
        {
            if (Data_ok == 1)
            {
                byte[] databuf = new byte[2];
                databuf[0] = 0x01;
                SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_CheckComp, databuf, 1);
            }
            else
            {
                textBox1.AppendText("请打开APP文件\r\n");//对话框追加显示数据
            }
        }
        /// <summary>
        /// 校验APP与打开文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button39_Click(object sender, EventArgs e)
        {
            if (Data_ok == 1)
            {
                byte[] databuf = new byte[2];
                databuf[0] = 0x02;
                SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_CheckComp, databuf, 1);
            }
            else
            {
                textBox1.AppendText("请打开APP文件\r\n");//对话框追加显示数据
            }
        }
        /// <summary>
        /// 校验BAK与APP
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button16_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x03;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_CheckComp, databuf, 1);

        }

        /// <summary>
        /// 加载BAK
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button13_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_StartLoad, databuf, 1);
        }


        /// <summary>
        /// 开始下载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button11_Click(object sender, EventArgs e)
        {
            if ((Data_ok > 0) && (BinSendFlag == false))
            {//必须要文件正常，且未开始升级

                BinSendFlag = true;//开始升级
                BinSendStep = 0;//升级步骤
                SendTIMM = 1000;//倒计时

                toolStripProgressBar1.Value = 0;
            }
        }


        /// <summary>
        /// 跳转至BOOT
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x02;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_Jump_Back, databuf, 1);
        }
        /// <summary>
        /// 跳转至APP
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button7_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_Jump_Back, databuf, 1);
        }
        #endregion

        #endregion

        //其他函数  
        //串口选择、打开、发送、数据接收
        //TCP连接、发送、接收
        //指令发送封包
        #region 其他函数


        /// <summary>
        /// 50毫秒定时延时
        /// 主要用于超时判定
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1_Tick(object sender, EventArgs e)
        {

            //发送数据后，开始倒计时
            //倒计时结束还未响应数据 则判定超时
            if (DataCountDown > 49)
            {
                DataCountDown -= 50;
            }

            //开始下载文件后开始计时
            if (BinSendFlag)
            {
                BinSendTim += 50;
            }

            //对于特殊指令，是需要在接收到响应后，立即回复数据
            //比如说，发送升级文件时，需要连续发送
            if (SendTIMM > 49)
            {
                SendTIMM -= 50;
            }

            toolStripStatusLabel2.Text = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");
        }


        /// <summary>
        /// 清除提示框内容
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button12_Click(object sender, EventArgs e)
        {
            textBox1.Clear();
        }
        /// <summary>
        /// TCP连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button6_Click(object sender, EventArgs e)
        {
            OpenCloseTcpServer();
        }
        /// <summary>
        /// 搜索可用串口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox1_Click(object sender, EventArgs e)
        {
            SearchSerialPort();
        }

        /// <summary>
        /// 按键5  打开/关闭串口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button5_Click(object sender, EventArgs e)
        {
            OpenCloseSerial();
        }

        /// <summary>
        /// 打开APP文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button10_Click(object sender, EventArgs e)
        {
            fileDialog.Multiselect = true;
            fileDialog.Title = "请选APP择文件";
            fileDialog.Filter = "bin文件|*.bin";

            if (fs != null)
            {
                fs.Close();
            }

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                fs = new FileStream(fileDialog.FileName, FileMode.Open);


                if (fs.Length > 2 * 1024)
                {//文件大小必须大于2k
                    Data_ok = 1;

                    Length_bin = fs.Length;//文件长度
                    Array.Clear(Data_bin, 0, Data_bin.Length);//先清空上次的缓存内容
                    fs.Read(Data_bin, 0, (int)fs.Length);

                    textBox1.AppendText("打开文件\r\n");//对话框追加显示数据
                    textBox1.AppendText(fs.Name);//对话框追加显示数据
                    textBox1.AppendText("\r\n文件大小:");//对话框追加显示数据
                    textBox1.AppendText((fs.Length / 1024.0).ToString());//对话框追加显示数据
                    textBox1.AppendText("kb\r\n");//对话框追加显示数据
                                                  //   label16.Text = (fs.Length / 1024.0).ToString() + "kb\r\n" + fs.Name ;
                                                  //Array.Copy(byteRead, Data_bin, byteRead.Length);
                                                  //Long_bin = byteRead.Length;

                    toolStripStatusLabel3.Text = "APP文件:" + fs.Name;
                }
                else
                {
                    textBox1.AppendText("打开文件\r\n");//对话框追加显示数据
                    textBox1.AppendText(fs.Name);//对话框追加显示数据
                    textBox1.AppendText("\r\n文件大小:");//对话框追加显示数据
                    textBox1.AppendText((fs.Length / 1024.0).ToString());//对话框追加显示数据
                    textBox1.AppendText("kb\r\n");//对话框追加显示数据


                    textBox1.AppendText("打开文件异常\r\n");//对话框追加显示数据

                    toolStripStatusLabel3.Text = "APP文件打开失败";



                }
            }
        }
        /// <summary>
        /// 打开固件文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button30_Click(object sender, EventArgs e)
        {
            fileDialog.Multiselect = true;
            fileDialog.Title = "请选择Firmware文件";
            fileDialog.Filter = "bin文件|*.bin";

            if (fs != null)
            {
                fs.Close();
            }

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                fs = new FileStream(fileDialog.FileName, FileMode.Open);


                if (fs.Length < 128 * 1024)
                {//文件大小必须大于2k
                    Data_ok = 2;

                    Length_bin = fs.Length;//文件长度
                    Array.Clear(Data_bin, 0, Data_bin.Length);//先清空上次的缓存内容
                    fs.Read(Data_bin, 0, (int)fs.Length);

                    textBox1.AppendText("打开Firmware文件\r\n");//对话框追加显示数据
                    textBox1.AppendText(fs.Name);//对话框追加显示数据
                    textBox1.AppendText("\r\n文件大小:");//对话框追加显示数据
                    textBox1.AppendText((fs.Length / 1024.0).ToString());//对话框追加显示数据
                    textBox1.AppendText("kb\r\n");//对话框追加显示数据

                    toolStripStatusLabel3.Text = "Firmware文件:" + fs.Name;

                }
                else
                {
                    textBox1.AppendText("打开Firmware文件\r\n");//对话框追加显示数据
                    textBox1.AppendText(fs.Name);//对话框追加显示数据
                    textBox1.AppendText("\r\n文件大小:");//对话框追加显示数据
                    textBox1.AppendText((fs.Length / 1024.0).ToString());//对话框追加显示数据
                    textBox1.AppendText("kb\r\n");//对话框追加显示数据


                    textBox1.AppendText("打开文件异常\r\n");//对话框追加显示数据


                    toolStripStatusLabel3.Text = "Firmware文件打开失败";

                }
            }
        }

        /// <summary>
        /// 设备号有改变时，进行更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox4_Click(object sender, EventArgs e)
        {
            try
            { CommentionAdd = DeviceNum[comboBox4.SelectedIndex]; }
            catch { }

        }
        private void comboBox4_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            { CommentionAdd = DeviceNum[comboBox4.SelectedIndex]; }

            catch { }

            if (comboBox4.SelectedIndex == 0)
            { //使能17号设备
                tabPage3.Parent = tabControl2;//显示系统升级
                tabPage5.Parent = tabControl2;//显示17号设备的配置界面
                tabPage6.Parent = null;//不显示18号设备的配置界面
            }
            else if (comboBox4.SelectedIndex == 1)
            { //使能18号设备
                tabPage3.Parent = tabControl2;//显示系统升级
                tabPage5.Parent = null;//不显示17号设备的配置界面
                tabPage6.Parent = tabControl2;//显示18号设备的配置界面

            }
            else if (comboBox4.SelectedIndex == 2)
            { //使能19号设备
                tabPage3.Parent = null;//不显示系统升级
                tabPage5.Parent = null;//不显示17号设备的配置界面
                tabPage6.Parent = null;//不显示18号设备的配置界面
            }
        }


        enum GNBootLoaderCmd_G
        {
            //指令分区
            BOOT_CDM_WRITE = 1,     // 升级相关的写
            BOOT_CDM_READ,     // 升级相关的读
            BOOT_SYSCONFIG_WRITE,//系统参数写入
            BOOT_SYSCONFIG_READ,//系统参数读取

            BOOT_SELFCONFIG_WRITE,//设备定制写
            BOOT_SELFCONFIG_READ,//设备定制读
            BOOT_CDM_CNT,



        }

        /// <summary>
        /// 当前上位机支持的指令数据
        /// </summary>
        enum GNBootLoaderCmd_Fun_G
        {
            //升级相关的写指令  01
            BOOT_WRITE_Rst = 1,     // 复位
            BOOT_WRITE_EraseChip,   // 擦除
            BOOT_WRITE_Jump_Back,   // 跳转App/BOOT
            BOOT_WRITE_Download,    // 升级文件下载相关
            BOOT_WRITE_CheckComp,   // 校验程序
            BOOT_WRITE_StartLoad,   // 加载新程序
            //BOOT_WRITE_ProgramDate, // 写入烧录日期
            //BOOT_WRITE_EquipmentSerial, // 写入设备序列号
            BOOT_WRITE_CNT,
            //升级相关的读指令  02
            BOOT_READ_Runstatus = 1, // 读取运行状态
            BOOT_READ_SoftVerion,    // 读取软件版本号
            BOOT_READ_HardVerion,    // 读取硬件版本号
            BOOT_READ_FirmwareVerion, // 读取固件版本号
                                      //      BOOT_READ_ProgramDate,   // 读取烧录日期
            BOOT_READ_DeviceDes,     // 读取设备描述符
            //BOOT_READ_EquipmentSerial,     // 读取设备序列号
            BOOT_READ_CNT,

            //系统参数的读写  03  04
            SYSCONFIG_U8_WRITE_READ1 = 1,//U8参数地址
            SYSCONFIG_U8_WRITE_READ2,
            SYSCONFIG_U8_WRITE_READ3,
            SYSCONFIG_U16_WRITE_READ1 = 11,//U16参数地址
            SYSCONFIG_U32_WRITE_READ1 = 21,//U32参数地址
            SYSCONFIG_BUF20_WRITE_READ1 = 31,//buf[20]参数地址
            SYSCONFIG_BUF20_WRITE_READ2,
            SYSCONFIG_BUF40_WRITE_READ1 = 41,//buf[40]参数地址
            SYSCONFIG_BUF40_WRITE_READ2,
            SYSCONFIG_BUF100_WRITE_READ1 = 51,//buf[100]参数地址
            SYSCONFIG_BUF100_WRITE_READ2,
            SYSCONFIG_BUF512_WRITE_READ1 = 61,//buf[512]参数地址
            SYSCONFIG_BUF512_WRITE_READ2,
            SYSCONFIG_BUF1024_WRITE_READ1 = 71,//buf[1024]参数地址
            SYSCONFIG_BUF1024_WRITE_READ2,
            SYSCONFIG_BUF2048_WRITE_READ1 = 81,//buf[2048]参数地址
            SYSCONFIG_BUF2048_WRITE_READ2,
            SYSCONFIG_TCP_IP_WRITE_READ = 171,//IP地址
            SYSCONFIG_TCP_MAC_WRITE_READ,//MAC地址
            SYSCONFIG_USART_BUND_WRITE_READ = 191,//串口波特率
            SYSCONFIG_DEVICE_ADD_WRITE_READ = 201,//设备号  预留


        }

        /// <summary>
        /// 指令发送模版
        /// </summary>
        /// <param name="add"></param>
        /// <param name="cmd0"></param>
        /// <param name="cmd1"></param>
        /// <param name="dat"></param>
        /// <param name="len"></param>
        private void SendCmd(byte add, GNBootLoaderCmd_G cmd0, GNBootLoaderCmd_Fun_G cmd1, byte[] dat, int len)
        {
            if (DataCountDownFly == true && Data_ok < 1)
            {
                DataCountDown = 0;

                DataCountDownFly = false;
                return;
            }
            if (connect_device < 1)
            {//设备未连接
                return;
            }

            byte[] databuf = new byte[4096];
            int datacount = 0;
            //设备号
            databuf[datacount++] = add;

            databuf[datacount++] = (byte)cmd0;//命令
            databuf[datacount++] = (byte)cmd1;//命令数据

            databuf[datacount++] = (byte)(len >> 8);//数据长度
            databuf[datacount++] = (byte)(len);

            for (int i = 0; i < len; i++)
            {
                databuf[datacount++] = dat[i];
            }

            databuf[datacount++] = checksum(databuf, datacount);

            SendBuf(databuf, datacount, connect_device);

            CommunicationAdd = add;
            CommunicationCmdLast1 = cmd0;
            CommunicationCmdLast2 = cmd1;

            DataCountDown = 2000;//2秒未响应判定超时  50ms的定时中断实时检测

            DataCountDownFly = true;//指令已发送标志位
        }


        /// <summary>
        /// 校验函数
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="datalength"></param>
        /// <returns></returns>
        private byte checksum(byte[] buf, int datalength)
        {
            byte result = 0;
            for (int i = 0; i < datalength; i++)
            {
                result += buf[i];
            }
            return result;
        }

        /// <summary>
        /// 数据发送
        /// </summary>
        /// <param name="data"></param>
        /// <param name="num"></param>
        /// <param name="connectdevice"></param>
        private void SendBuf(byte[] data, int num, byte connectdevice)
        {
            if (connectdevice == 1)
            {//串口发送
                SendDataSerial(data, num);
            }
            else if (connectdevice == 2)
            {
                TcpSendData(data, num);
            }
        }

        /// <summary>
        /// 搜索当前可用串口
        /// </summary>
        private void SearchSerialPort()
        {
            comboBox1.Items.Clear();

            //获取电脑当前可用串口并添加到选项列表中
            comboBox1.Items.AddRange(SerialPort.GetPortNames());

            try
            {
                comboBox1.SelectedIndex = 0;
            }
            catch
            {
                MessageBox.Show("没有可用串口", "警告提示");
            }
        }
        /// <summary>
        /// 串口打开/关闭
        /// </summary>
        private void OpenCloseSerial()
        {
            try
            {
                if (serialPort1.IsOpen)
                {
                    serialPort1.Close();


                }
                else
                {
                    serialPort1.BaudRate = Convert.ToInt32(comboBox2.Text);
                    serialPort1.PortName = comboBox1.Text;
                    serialPort1.DataBits = 8;
                    serialPort1.StopBits = StopBits.One;   //停止位;
                    serialPort1.DataReceived += new SerialDataReceivedEventHandler(sp_DataReceived);
                    serialPort1.Open();


                }
            }
            catch { }


            if (serialPort1.IsOpen)
            {
                comboBox1.Enabled = false;
                comboBox2.Enabled = false;

                button5.Text = "关闭";

                connect_device = 1;
                //串口打开后，不允许再操作TCP
                tabPage2.Parent = null;//隐藏 TCP配置界面
                //       tabPage2.Visible = false;

                groupBox1.Enabled = true;//显示设备号选择


                //连接状态显示
                toolStripStatusLabel1.Text = "串口已连接";

                textBox1.AppendText("设备已经串口连接\r\n");//对话框追加显示数据
                textBox1.AppendText("当前是多设备bootloader升级系统\r\n");//对话框追加显示数据
            }
            else
            {
                comboBox1.Enabled = true;
                comboBox2.Enabled = true;

                button5.Text = "打开";

                connect_device = 0;
                //串口关闭后，可以重新选择TCP
                tabPage2.Parent = tabControl1;//显示,TCP配置界面

                groupBox1.Enabled = false;//隐藏设备号选择
                tabPage3.Parent = null;//不显示系统升级
                tabPage5.Parent = null;//不显示17号设备的配置界面
                tabPage6.Parent = null;//不显示18号设备的配置界面

                comboBox4.Text = null;
                //   tabPage2.Visible = true;


                //连接状态显示
                toolStripStatusLabel1.Text = "设备未连接";



            }
        }
        /// <summary>
        /// 串口数据接收
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (!serialPort1.IsOpen)//串口在关闭时不接收数据 为了防止关闭串口时卡死的问题
                {
                    serialPort1?.DiscardInBuffer();//丢弃接收缓冲区数据
                    return;
                }
                if (serialPort1.BytesToRead < 4)
                {
                    return;
                }

                Thread.Sleep(25);//延时25ms 等待接收完数据

                TCPSerialRecveLength = serialPort1.BytesToRead;//接收数据的字节数
                serialPort1.Read(TCPSerialReceiveBuf, 0, TCPSerialRecveLength); //读取所接收到的数据 

                TCPSerialReceiveFlag = true;
            }
            catch
            {

            }
        }
        /// <summary>
        /// 串口发送数据
        /// </summary>
        /// <param name="data"></param>
        /// <param name="num"></param>
        private void SendDataSerial(byte[] data, int num)
        {
            if (serialPort1.IsOpen && num > 0 && data != null)
            {
                serialPort1.Write(data, 0, num);
            }
        }

        /// <summary>
        /// 打开/关闭TCP连接
        /// </summary>
        private void OpenCloseTcpServer()
        {
            RemoteIp = textBox4.Text;//获取IP地址
            RemotePort = Convert.ToInt32(textBox3.Text);          //获取端口号

            if (button6.Text == "连接")
            {
                //IP地址 和 端口号输入不为空
                if (string.IsNullOrEmpty(textBox3.Text) == false && string.IsNullOrEmpty(textBox4.Text) == false)
                {
                    if (ConnectToServer(true))
                    {

                    }
                }
                else
                {
                    MessageBox.Show("IP地址或端口号错误!", "提示");
                }
            }
            else
            {
                if (ConnectToServer(false))
                {

                }
            }

            Thread.Sleep(100);//延时200ms 等待连接状态

            if (IsConnected)
            {

                button6.Text = "断开";
                textBox3.Enabled = false;
                textBox4.Enabled = false;
                connect_device = 2;
                //TCP连接成功  不允许再操作串口
                tabPage1.Parent = null;//显示,串口配置界面

                groupBox1.Enabled = true;//隐藏设备号选择

                //连接状态显示
                toolStripStatusLabel1.Text = "TCP已连接";
            }
            else
            {

                button6.Text = "连接";
                textBox3.Enabled = true;
                textBox4.Enabled = true;
                connect_device = 0;
                //TCP连接成功  不允许再操作串口
                tabPage1.Parent = tabControl1;//显示,串口配置界面

                groupBox1.Enabled = false;//隐藏设备号选择
                tabPage3.Parent = null;//不显示系统升级
                tabPage5.Parent = null;//不显示17号设备的配置界面
                tabPage6.Parent = null;//不显示18号设备的配置界面

                comboBox4.Text = null;


                //连接状态显示
                toolStripStatusLabel1.Text = "设备未连接";
            }
        }
        /// <summary>
        /// 打开TCP连接
        /// </summary>
        private static bool ConnectToServer(bool ok)
        {
            try
            {
                //开始连接
                if (ok == true)
                {
                    //初始化TCP客户端对象
                    tcpClient = new TcpClient();  //Tcp客户端模型
                    tcpClient.BeginConnect(RemoteIp, RemotePort, new AsyncCallback(AsynConnect), tcpClient);
                }
                else
                {
                    tcpClient.GetStream().Close();
                    tcpClient.Close();
                    //关闭连接后马上更新连接状态标志
                    IsConnected = false;
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("TcpClientBusiness", "ConnectToServer|异常消息：" + ex.Message.ToString());
            }
            return false;
        }

        /// <summary>
        /// 异步连接
        /// </summary>
        /// <param name="iar"></param>
        private static void AsynConnect(IAsyncResult iar)
        {
            try
            {
                //连接成功
                tcpClient.EndConnect(iar);
                //连接成功标志
                IsConnected = true;
                networkStream = tcpClient.GetStream();
                byte[] TempBytes = new byte[ReceiveDataBufLength];
                //开始异步读取返回数据
                networkStream.BeginRead(TempBytes, 0, TempBytes.Length, new AsyncCallback(AsynReceiveData), TempBytes);
            }
            catch (Exception ex)
            {
                //    MessageBox.Show("TcpClientBusiness", "AsynConnect|异常消息：" + ex.Message.ToString());
            }
        }
        /// <summary>
        /// 发送数据
        /// <param name="SendBytes">需要发送的数据</param>
        /// </summary>
        private static void TcpSendData(byte[] SendBytes, int num)
        {
            try
            {
                if (networkStream.CanWrite && SendBytes != null && num > 0)
                {
                    //发送数据
                    networkStream.Write(SendBytes, 0, num);
                    networkStream.Flush();
                }
            }
            catch (Exception ex)
            {
                if (tcpClient != null)
                {
                    tcpClient.Close();
                    //关闭连接后马上更新连接状态标志
                    IsConnected = false;
                }
                //     MessageBox.Show("TcpClientBusiness", "SendData|异常消息：" + ex.Message.ToString());
            }
        }

        /// <summary>
        /// 异步接受数据
        /// </summary>
        /// <param name="iar"></param>
        private static void AsynReceiveData(IAsyncResult iar)
        {
            byte[] CurrentBytes = (byte[])iar.AsyncState;
            try
            {
                //结束了本次数据接收
                TCPSerialRecveLength = networkStream.EndRead(iar);
                //int num = networkStream.EndRead(iar);
                //这里展示结果为InfoModel的CurrBytes属性，将返回的数据添加至返回数据容器中

                TCPSerialReceiveBuf = CurrentBytes;
                //ResponseBytes.Add(CurrentBytes);

                TCPSerialReceiveFlag = true;

                //处理结果后马上启动数据异步读取【目前我每条接收的字节数据长度不会超过1024】
                byte[] NewBytes = new byte[ReceiveDataBufLength];
                networkStream.BeginRead(NewBytes, 0, NewBytes.Length, new AsyncCallback(AsynReceiveData), NewBytes);

            }
            catch (Exception ex)
            {
                //    MessageBox.Show("TcpClientBusiness", "AsynReceiveData|异常消息：" + ex.Message.ToString());
            }
        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }


        private void label15_Click(object sender, EventArgs e)
        {

        }

        private void textBox11_TextChanged(object sender, EventArgs e)
        {
        }

        #endregion

        #region 17# 正点原子F407

        /// <summary>
        /// 17#   version读取
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button44_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SELFCONFIG_READ, (GNBootLoaderCmd_Fun_G)1, databuf, 0);
        }
        /// <summary>
        /// 17#   version写入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button45_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = Convert.ToByte(textBox17.Text);

            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SELFCONFIG_WRITE, (GNBootLoaderCmd_Fun_G)1, databuf, 1);
        }
        /// <summary>
        /// 17#   version2读取
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button32_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SELFCONFIG_READ, (GNBootLoaderCmd_Fun_G)2, databuf, 0);
        }
        /// <summary>
        /// 17#   version2写入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button37_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            UInt16 addd = Convert.ToUInt16(textBox16.Text);

            databuf[0] = (byte)(addd);
            databuf[1] = (byte)(addd >> 8);
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SELFCONFIG_WRITE, (GNBootLoaderCmd_Fun_G)2, databuf, 2);
        }

        /// <summary>
        /// 17# 读取U8
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button56_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_U8_WRITE_READ1, databuf, 0);
        }
        /// <summary>
        /// 17#  写入U8
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button57_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = Convert.ToByte(textBox15.Text);

            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_U8_WRITE_READ1, databuf, 1);
        }
        /// <summary>
        /// 17#  读取U16
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button54_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_U16_WRITE_READ1, databuf, 0);
        }
        /// <summary>
        /// 17#  写入U16
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button55_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            UInt16 addd = Convert.ToUInt16(textBox14.Text);

            databuf[0] = (byte)(addd);
            databuf[1] = (byte)(addd >> 8);
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_U16_WRITE_READ1, databuf, 2);
        }
        /// <summary>
        /// 17#  读取u32
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button52_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_U32_WRITE_READ1, databuf, 0);
        }
        /// <summary>
        /// 17#  写入U32
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button53_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[4];
            UInt32 addd = Convert.ToUInt32(textBox13.Text);
            databuf[3] = (byte)(addd >> 24);
            databuf[2] = (byte)(addd >> 16);
            databuf[1] = (byte)(addd >> 8);
            databuf[0] = (byte)(addd);
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_U32_WRITE_READ1, databuf, 4);
        }
        /// <summary>
        /// 17#  读取烧录日期
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button33_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF40_WRITE_READ1, databuf, 0);
        }
        /// <summary>
        /// 17#  写入烧录日期
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button36_Click(object sender, EventArgs e)
        {
            DateTime dt = DateTime.Now;
            byte[] databuf = System.Text.Encoding.Default.GetBytes(dt.ToString());
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF40_WRITE_READ1, databuf, databuf.Length);
        }
        /// <summary>
        /// 17#  读取IP地址
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button50_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_IP_WRITE_READ, databuf, 0);
        }
        /// <summary>
        /// 17#  写入IP地址
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button51_Click(object sender, EventArgs e)
        {
            try
            {
                string[] words = textBox12.Text.Split('.');
                byte[] databuf = new byte[4];

                databuf[0] = Convert.ToByte(words[0]);
                databuf[1] = Convert.ToByte(words[1]);
                databuf[2] = Convert.ToByte(words[2]);
                databuf[3] = Convert.ToByte(words[3]);
                SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_IP_WRITE_READ, databuf, 4);
            }
            catch
            { }
        }
        /// <summary>
        /// 17#  读取MAC地址
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button46_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_MAC_WRITE_READ, databuf, 0);
        }
        /// <summary>
        ///17#  写入MAC地址
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button47_Click(object sender, EventArgs e)
        {
            try
            {
                string[] words = textBox11.Text.Split('.');
                byte[] databuf = new byte[6];


                databuf[0] = Convert.ToByte(words[0]);
                databuf[1] = Convert.ToByte(words[1]);
                databuf[2] = Convert.ToByte(words[2]);
                databuf[3] = Convert.ToByte(words[3]);
                databuf[4] = Convert.ToByte(words[4]);
                databuf[5] = Convert.ToByte(words[5]);
                SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_MAC_WRITE_READ, databuf, 6);
            }
            catch
            { }
        }
        /// <summary>
        /// 17#  读取设备序列号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button34_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF20_WRITE_READ1, databuf, 0);
        }
        /// <summary>
        /// 17#  写入设备序列号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button38_Click(object sender, EventArgs e)
        {
            if ((textBox10.TextLength < 20) && (textBox10.TextLength > 5))
            {
                SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF20_WRITE_READ1, Encoding.Default.GetBytes(textBox10.Text), textBox10.TextLength);
            }
            else
            {
                MessageBox.Show("字符长度不合适,宽度为5-20个字符长度!", "警告");
            }
        }

        #endregion


        #region 18# STM32项目经验学习

        /// <summary>
        /// 18# 设备序列号读取
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button64_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF20_WRITE_READ1, databuf, 0);
        }

        /// <summary>
        /// 18# 设备序列号写入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button65_Click(object sender, EventArgs e)
        {
            if ((textBox21.TextLength < 20) && (textBox21.TextLength > 5))
            {
                SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF20_WRITE_READ1, Encoding.Default.GetBytes(textBox21.Text), textBox21.TextLength);
            }
            else
            {
                MessageBox.Show("字符长度不合适,宽度为5-20个字符长度!", "警告");
            }
        }
        /// <summary>
        /// 18#  读取version
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button68_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SELFCONFIG_READ, (GNBootLoaderCmd_Fun_G)1, databuf, 0);
        }
        /// <summary>
        /// 18#  写入version
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button69_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = Convert.ToByte(textBox23.Text);

            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SELFCONFIG_WRITE, (GNBootLoaderCmd_Fun_G)1, databuf, 1);
        }
        /// <summary>
        /// 18#  读取version2
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button66_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SELFCONFIG_READ, (GNBootLoaderCmd_Fun_G)2, databuf, 0);
        }
        /// <summary>
        /// 18#  读取version2
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button67_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            UInt16 addd = Convert.ToUInt16(textBox22.Text);

            databuf[0] = (byte)(addd);
            databuf[1] = (byte)(addd >> 8);
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SELFCONFIG_WRITE, (GNBootLoaderCmd_Fun_G)2, databuf, 2);
        }
        /// <summary>
        /// 18# 读取U8
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button62_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_U8_WRITE_READ1, databuf, 0);
        }
        /// <summary>
        /// 18#  写入U8
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button63_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = Convert.ToByte(textBox20.Text);

            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_U8_WRITE_READ1, databuf, 1);
        }
        /// <summary>
        /// 18# 读取U16
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button60_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_U16_WRITE_READ1, databuf, 0);
        }
        /// <summary>
        /// 18#  写入U16
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button61_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            UInt16 addd = Convert.ToUInt16(textBox19.Text);

            databuf[0] = (byte)(addd);
            databuf[1] = (byte)(addd >> 8);
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_U16_WRITE_READ1, databuf, 2);
        }
        /// <summary>
        /// 18# 读取U32
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button58_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[2];
            databuf[0] = 0x01;
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ, GNBootLoaderCmd_Fun_G.SYSCONFIG_U32_WRITE_READ1, databuf, 0);
        }
        /// <summary>
        /// 18#  写入U32
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button59_Click(object sender, EventArgs e)
        {
            byte[] databuf = new byte[4];
            UInt32 addd = Convert.ToUInt32(textBox18.Text);
            databuf[3] = (byte)(addd >> 24);
            databuf[2] = (byte)(addd >> 16);
            databuf[1] = (byte)(addd >> 8);
            databuf[0] = (byte)(addd);
            SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE, GNBootLoaderCmd_Fun_G.SYSCONFIG_U32_WRITE_READ1, databuf, 4);
        }
        #endregion


    }
}