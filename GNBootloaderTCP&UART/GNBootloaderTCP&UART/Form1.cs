
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Text.Unicode;

namespace GNBootloaderTCP_UART
{
    public partial class Form1 : Form
    {
        //ȫ�ֱ�������
        #region
        //���ݽ��ճ���
        private const int ReceiveDataBufLength = 4096;
        private byte connect_device = 0;//��������ѡ��
                                        //1:����
                                        //2:TCP

        //�������
        private static SerialPort serialPort1 = new SerialPort();//����
        //int SerialRecveLength;//�������ݽ��ճ���
        //byte[] SerialReceiveBuf = new byte[ReceiveDataBufLength];//�������ݻ��泤��
        //bool SerialReceiveFlag = false;//���ڽ������

        //TCP�ͻ���
        private static TcpClient tcpClient;  //Tcp�ͻ���ģ��
        private static NetworkStream networkStream;// �������������
                                                   //  public static List<byte[]> ResponseBytes = new List<byte[]>();// ��������
                                                   //public static byte[] ResponseBytes = new byte[ReceiveDataBufLength];// ��������
        private static string RemoteIp = string.Empty;// Զ�̷���IP��ַ
        private static int RemotePort = -1;// Զ�̷���IP��ַ��Ӧ�˿�
        private static bool IsConnected = false;// �Ƿ�����

        //���ڡ�TCP�ͻ���
        //���ݽ��ճ��ȡ���־λ������
        private static int TCPSerialRecveLength;//�������ݽ��ճ���
        private static byte[] TCPSerialReceiveBuf = new byte[ReceiveDataBufLength];//�������ݻ��泤��
        private static bool TCPSerialReceiveFlag = false;//���ڽ������
        private const int TCPSerialBufferOffset = 5;//���յ������Ժ�ƫ��5����ַ�������Ч����

        //��¼����ʵʱͨѶ������
        //���ڽ�����������ʱ���ж��Ƿ�ʱ�������ȹ���
        private byte CommunicationAdd = 0;//��¼��ǰָ��ĵ�ַ
        private GNBootLoaderCmd_G CommunicationCmdLast1 = 0;//��¼��ǰָ��
        private GNBootLoaderCmd_Fun_G CommunicationCmdLast2 = 0;//��¼��ǰָ������
        int DataCountDown = 0;//�ȴ����յ���ʱ
        bool DataCountDownFly = false;//�ȴ����ձ�־λ

        //�豸��  ��ʾ��ǰ���ӵ��豸
        private byte CommentionAdd = 0;

        //���ڴ������ļ�ʹ��
        private OpenFileDialog fileDialog = new OpenFileDialog();
        private FileStream fs;
        private byte Data_ok = 0;
        private byte[] Data_bin = new byte[1024 * 1024];
        private long Length_bin = 0;//�ļ���С
        private bool BinSendFlag = false;//�Ƿ�ʼ����
        byte BinSendStep = 0;//���Ͳ���
        private long BinSendRemanet = 0;//ʣ�����ݸ���
        private int BinSendNumt = 0;//���ͼ���
        private int BinSendTim = 0;//���ͼ�ʱ
        private int SendTIMM = 1000;//���ͼ��ʱ��
        private const int SendTIMM_CONST = 1000;//���ͼ���Ĺ̶�ʱ��

        //�߳�����
        //���ڴ����������
        private Thread thread;//�߳�


        //Ĭ�ϵ��豸��
        private byte[] DeviceNum = { 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 };
        #endregion

        public Form1()
        {
            InitializeComponent();
        }
        /// <summary>
        /// �����ʼ����
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {
            SearchSerialPort();//������ǰ���ô���
            comboBox2.SelectedIndex = 7;//ѡ��115200������

            //�߳�����
            thread = new Thread(new ThreadStart(DoWork));
            thread.IsBackground = true;
            thread.Start();


            //���δ˲�����ʾ��ֻ������д����ο�         
            tabPage4.Parent = null;//�˲���һֱ����ʾ

            //�򿪴��ڣ�ѡ���豸��֮�󣬻�ʹ��
            tabPage3.Parent = null;//����ʾϵͳ����
            tabPage5.Parent = null;//����ʾ17���豸�����ý���
            tabPage6.Parent = null;//����ʾ18���豸�����ý���

            //�豸��ѡ�� ��ʹ��
            groupBox1.Enabled = false;


            toolStripStatusLabel2.Alignment = ToolStripItemAlignment.Right;
            toolStripStatusLabel3.Alignment = ToolStripItemAlignment.Right;
            toolStripStatusLabel3.Text = "";

            //����APP������
            button8.Enabled = false;
        }

        /// <summary>
        /// �߳� ���ڴ����������
        /// </summary>
        private void DoWork()
        {
            CheckForIllegalCrossThreadCalls = false;

            //��ʱʹ��
            byte[] strsttt = new byte[100];
            byte[] bindatabuf = new byte[4096];
            while (true)
            {
                //�豸�ڽ��д��������ļ�����Ҫ��������
                if (BinSendFlag == true && Data_ok > 0)
                {
                    switch (BinSendStep)
                    {
                        case 0://��������
                            {

                                if (SendTIMM < 60)
                                {//��ʼ���������Ժ�
                                 //���Ϳ�ʼ����ָ��

                                    byte[] databuf = new byte[2];

                                    if (Data_ok == 1)
                                    {//APP�����ļ�
                                        databuf[0] = 0x01;
                                    }
                                    else if (Data_ok == 2)
                                    {//�̼������ļ�
                                        databuf[0] = 0x11;
                                    }
                                    SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_Download, databuf, 1);

                                    BinSendRemanet = Length_bin;//ʣ������
                                    BinSendNumt = 0;//���ͼ���


                                    BinSendTim = 0;//���¿�ʼ��ʱ
                                    SendTIMM = SendTIMM_CONST;//���¿�ʼ����ʱ1��

                                    BinSendStep++;
                                }
                            }
                            break;
                        case 2://���������ļ�
                            {
                                if ((DataCountDown < 201) && (DataCountDown > 100))
                                {//���յ�����


                                    if (Data_ok == 1)
                                    {//APP�����ļ�
                                        bindatabuf[0] = 0x02;//��������
                                    }
                                    else if (Data_ok == 2)
                                    {//�̼������ļ�
                                        bindatabuf[0] = 0x12;//��������
                                    }

                                    if (BinSendRemanet > 512)
                                    {//ʣ���ֽڴ���512���ֽ�

                                        //ÿ�ζ�ȡ512���ֽ�
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
                                        //��ȡʣ���ֽڸ���
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
                                {//APP�����ļ�
                                    databuf1[0] = 0x03;
                                }
                                else if (Data_ok == 2)
                                {//�̼������ļ�
                                    databuf1[0] = 0x13;
                                }

                                SendCmd(CommentionAdd, GNBootLoaderCmd_G.BOOT_CDM_WRITE, GNBootLoaderCmd_Fun_G.BOOT_WRITE_Download, databuf1, 1);

                                BinSendFlag = false;

                                textBox1.AppendText("��ʱ��" + BinSendTim.ToString() + "\r\n");//�Ի���׷����ʾ����

                                //�ر��ļ�
                                Array.Clear(Data_bin, 0, Data_bin.Length);//������ϴεĻ�������
                                fs.Close();
                                toolStripStatusLabel3.Text = "�ļ��������";
                                toolStripProgressBar1.Value = 0;

                                Data_ok = 0;
                            }
                            break;
                    }
                }

                //��ʱ���
                //���ݷ�����ʱ  �ж��쳣
                if (DataCountDownFly == true)
                {
                    if (DataCountDown < 51)
                    {
                        DataCountDownFly = false;
                        textBox1.AppendText("�豸��ʱ\r\n");//�Ի���׷����ʾ����
                        //��ʱ�������ļ����ٷ���
                        if (BinSendFlag == true)
                        {
                            BinSendFlag = false;
                        }
                    }
                    Thread.Sleep(5);//��ʱ5ms �ȴ�����������
                }
                else
                {
                    Thread.Sleep(2);//��ʱ2ms �ȴ�����������
                }

                //�ȴ���������  ͬʱ���յ�������
                if (TCPSerialReceiveFlag == true)
                {
                    TCPSerialReceiveFlag = false;//������ݽ��ձ�־λ

                    if (CommunicationAdd == TCPSerialReceiveBuf[0])
                    { //��ַ�ж�
                        if (checksum(TCPSerialReceiveBuf, TCPSerialRecveLength - 1) == TCPSerialReceiveBuf[TCPSerialRecveLength - 1])
                        { //У��λ���

                            TCPSerialReceiveBuf[1] -= 0x80;
                            if ((byte)CommunicationCmdLast2 == TCPSerialReceiveBuf[2] && ((byte)(CommunicationCmdLast1) == TCPSerialReceiveBuf[1]))
                            { //ָ���ָ�����ݼ��
                              //��������Ӧ���뷢�͵�ָ����һ�µ�

                                //��Ч�����ݷ���
                                DataCountDownFly = false;
                                //�������ݵĳ���
                                int datlen = (int)((TCPSerialReceiveBuf[3] << 8) + TCPSerialReceiveBuf[4]);

                                switch ((GNBootLoaderCmd_G)TCPSerialReceiveBuf[1])
                                {
                                    case GNBootLoaderCmd_G.BOOT_CDM_WRITE://0x01  ������ص�дָ��
                                        {
                                            switch ((GNBootLoaderCmd_Fun_G)TCPSerialReceiveBuf[2])
                                            {
                                                case GNBootLoaderCmd_Fun_G.BOOT_WRITE_Rst://��λ

                                                    if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x01)
                                                    {
                                                        textBox1.AppendText("�豸��λ\r\n");//�Ի���׷����ʾ����
                                                    }
                                                   else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x02)
                                                    {
                                                        textBox1.AppendText("�豸��λʧ��\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_WRITE_EraseChip://����

                                                    if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x01)
                                                    {
                                                        textBox1.AppendText("��ʼ����BAK��\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x02)
                                                    {
                                                        textBox1.AppendText("����BAK�����\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x03)
                                                    {
                                                        textBox1.AppendText("��ʼ����APP��\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x04)
                                                    {
                                                        textBox1.AppendText("����APP�����\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_WRITE_Jump_Back://��ת

                                                    if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x01)
                                                    {
                                                        textBox1.AppendText("��ת��APP\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x02)
                                                    {
                                                        textBox1.AppendText("��ת��APPʧ��\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x03)
                                                    {
                                                        textBox1.AppendText("��ת��BOOT\r\n");//�Ի���׷����ʾ����                                                  
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x04)
                                                    {
                                                        textBox1.AppendText("��ת��BOOTʧ��\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_WRITE_Download://����

                                                    if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x01)
                                                    {
                                                        if (BinSendStep == 1)
                                                        {
                                                            BinSendStep++;
                                                            DataCountDownFly = true;

                                                            DataCountDown = 249;//���������ݺ�  50ms������һ������

                                                        }
                                                        textBox1.AppendText("��ʼ����APP�ļ�\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x02)
                                                    {
                                                        DataCountDownFly = true;

                                                        DataCountDown = 249;//���������ݺ�  50ms������һ������

                                                        //       textBox1.AppendText("��������\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x03)
                                                    {
                                                        textBox1.AppendText("APP��������\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x11)
                                                    {
                                                        if (BinSendStep == 1)
                                                        {
                                                            BinSendStep++;
                                                            DataCountDownFly = true;

                                                            DataCountDown = 249;//���������ݺ�  50ms������һ������

                                                        }
                                                        textBox1.AppendText("��ʼ���ع̼��ļ�\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x12)
                                                    {
                                                        DataCountDownFly = true;

                                                        DataCountDown = 249;//���������ݺ�  50ms������һ������

                                                        //       textBox1.AppendText("��������\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x13)
                                                    {
                                                        textBox1.AppendText("�̼���������\r\n");//�Ի���׷����ʾ����
                                                    }

                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_WRITE_CheckComp://У�����

                                                    if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x01)
                                                    {
                                                        textBox1.AppendText("��ʼУ��BAK\r\n");//�Ի���׷����ʾ����
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
                                                        //textBox1.AppendText("bakcrc    " + bakcrc.ToString("X2") + "\r\n");//�Ի���׷����ʾ����
                                                        //textBox1.AppendText("bak_len   " + bak_len.ToString() + "\r\n");//�Ի���׷����ʾ����

                                                        int reclen = 0;

                                                        reclen = TCPSerialReceiveBuf[TCPSerialBufferOffset + 5];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 4];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 3];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 2];

                                                        //textBox1.AppendText("TCPSerialReceiveBuf[TCPSerialBufferOffset + 1]     " + TCPSerialReceiveBuf[TCPSerialBufferOffset + 1].ToString("X2") + "\r\n");//�Ի���׷����ʾ����
                                                        //textBox1.AppendText("reclen     " + reclen.ToString() + "\r\n");//�Ի���׷����ʾ����

                                                        if ((bakcrc == TCPSerialReceiveBuf[TCPSerialBufferOffset + 1])
                                                            && (bak_len == reclen)

                                                            )
                                                        {
                                                            textBox1.AppendText("BAKУ�����:BAK��򿪵��ļ�һ��\r\n");//�Ի���׷����ʾ����
                                                        }
                                                        else
                                                        {
                                                            textBox1.AppendText("BAKУ�����:BAK��򿪵��ļ��в���\r\n");//�Ի���׷����ʾ����
                                                        }
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x03)
                                                    {
                                                        textBox1.AppendText("��ʼУ��APP\r\n");//�Ի���׷����ʾ����
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
                                                            textBox1.AppendText("APPУ�����:APP��򿪵��ļ�һ��\r\n");//�Ի���׷����ʾ����
                                                        }
                                                        else
                                                        {
                                                            textBox1.AppendText("APPУ�����:APP��򿪵��ļ��в���\r\n");//�Ի���׷����ʾ����
                                                        }
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x05)
                                                    {
                                                        textBox1.AppendText("��ʼУ��BAK��APP\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 2 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x06)
                                                    {
                                                        if (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] == 0)
                                                        {
                                                            textBox1.AppendText("BAK��APPУ�����:BAK��APPһ��\r\n");//�Ի���׷����ʾ����
                                                        }
                                                        else
                                                        {
                                                            textBox1.AppendText("BAK��APPУ�����:BAK��APP�в���\r\n");//�Ի���׷����ʾ����
                                                        }
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x11)
                                                    {
                                                        textBox1.AppendText("��ʼУ��Firmware\r\n");//�Ի���׷����ʾ����
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
                                                        //textBox1.AppendText("bakcrc    " + bakcrc.ToString("X2") + "\r\n");//�Ի���׷����ʾ����
                                                        //textBox1.AppendText("bak_len   " + bak_len.ToString() + "\r\n");//�Ի���׷����ʾ����

                                                        int reclen = 0;

                                                        reclen = TCPSerialReceiveBuf[TCPSerialBufferOffset + 5];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 4];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 3];
                                                        reclen <<= 8;
                                                        reclen += TCPSerialReceiveBuf[TCPSerialBufferOffset + 2];

                                                        //textBox1.AppendText("TCPSerialReceiveBuf[TCPSerialBufferOffset + 1]     " + TCPSerialReceiveBuf[TCPSerialBufferOffset + 1].ToString("X2") + "\r\n");//�Ի���׷����ʾ����
                                                        //textBox1.AppendText("reclen     " + reclen.ToString() + "\r\n");//�Ի���׷����ʾ����

                                                        if ((bakcrc == TCPSerialReceiveBuf[TCPSerialBufferOffset + 1])
                                                            && (bak_len == reclen)

                                                            )
                                                        {
                                                            textBox1.AppendText("FirmwareУ�����:Firmware��򿪵��ļ�һ��\r\n");//�Ի���׷����ʾ����
                                                        }
                                                        else
                                                        {
                                                            textBox1.AppendText("FirmwareУ�����:Firmware��򿪵��ļ��в���\r\n");//�Ի���׷����ʾ����
                                                        }
                                                    }

                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_WRITE_StartLoad://BAK����

                                                    if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x01)
                                                    {
                                                        textBox1.AppendText("��ʼ����APP��\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x02)
                                                    {
                                                        textBox1.AppendText("����APP�����\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x03)
                                                    {
                                                        textBox1.AppendText("��ʼ����BAK��\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    else if (datlen == 1 && TCPSerialReceiveBuf[TCPSerialBufferOffset + 0] == 0x04)
                                                    {
                                                        textBox1.AppendText("����BAK�����\r\n");//�Ի���׷����ʾ����
                                                    }
                                                    break;

                                            }
                                        }
                                        break;
                                    case GNBootLoaderCmd_G.BOOT_CDM_READ://0x02  ������صĶ�ָ��
                                        {
                                            Array.Clear(strsttt, 0, strsttt.Length);
                                            switch ((GNBootLoaderCmd_Fun_G)TCPSerialReceiveBuf[2])
                                            {
                                                case GNBootLoaderCmd_Fun_G.BOOT_READ_Runstatus://��ȡ����״̬

                                                    for (int loc = 0; loc < datlen; loc++)
                                                    {
                                                        strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                    }

                                                    textBox1.AppendText(System.Text.Encoding.Default.GetString(strsttt));//�Ի���׷����ʾ����       
                                                    textBox1.AppendText("\r\n");
                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_READ_SoftVerion://��ȡ����汾��

                                                    for (int loc = 0; loc < datlen; loc++)
                                                    {
                                                        strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                    }

                                                    textBox1.AppendText(System.Text.Encoding.Default.GetString(strsttt));//�Ի���׷����ʾ����       
                                                    textBox1.AppendText("\r\n");
                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_READ_HardVerion://��ȡӲ���汾��

                                                    for (int loc = 0; loc < datlen; loc++)
                                                    {
                                                        strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                    }

                                                    textBox1.AppendText(System.Text.Encoding.Default.GetString(strsttt));//�Ի���׷����ʾ����       
                                                    textBox1.AppendText("\r\n");
                                                    break;
                                                case GNBootLoaderCmd_Fun_G.BOOT_READ_FirmwareVerion://��ȡ�̼��汾��

                                                    for (int loc = 0; loc < datlen; loc++)
                                                    {
                                                        strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                    }

                                                    textBox1.AppendText(System.Text.Encoding.Default.GetString(strsttt));//�Ի���׷����ʾ����       
                                                    textBox1.AppendText("\r\n");
                                                    break;

                                                case GNBootLoaderCmd_Fun_G.BOOT_READ_DeviceDes://��ȡ�豸����

                                                    for (int loc = 0; loc < datlen; loc++)
                                                    {
                                                        strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                    }

                                                    textBox1.AppendText(Encoding.Default.GetString(strsttt));//�Ի���׷����ʾ����       
                                                    textBox1.AppendText("\r\n");
                                                    break;

                                            }
                                        }
                                        break;
                                    case GNBootLoaderCmd_G.BOOT_SYSCONFIG_WRITE://0x03  ϵͳ����д��
                                    case GNBootLoaderCmd_G.BOOT_SYSCONFIG_READ://0x04  ϵͳ������ȡ
                                        {
                                            switch (TCPSerialReceiveBuf[0])
                                            {//���ݵ�ǰ�豸�ţ���ʾ�ڲ�ͬ������
                                                case 1:
                                                    {
                                                        switch ((GNBootLoaderCmd_Fun_G)TCPSerialReceiveBuf[2])
                                                        {
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U8_WRITE_READ1://U8  
                                                                {
                                                                    textBox5.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox5.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U16_WRITE_READ1://U16
                                                                {
                                                                    UInt16 add = (UInt16)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8));


                                                                    textBox6.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox6.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U32_WRITE_READ1://U32
                                                                {
                                                                    UInt32 add = (UInt32)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8) + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 2] << 16) + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 3] << 24));


                                                                    textBox7.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox7.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF20_WRITE_READ1://buf[20]  ���к�
                                                                Array.Clear(strsttt, 0, strsttt.Length);
                                                                for (int loc = 0; loc < datlen; loc++)
                                                                {
                                                                    strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                                }
                                                                textBox2.Text = System.Text.Encoding.Default.GetString(strsttt);

                                                                textBox1.AppendText(textBox2.Text);//�Ի���׷����ʾ����       
                                                                textBox1.AppendText("\r\n");

                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF40_WRITE_READ1://buf[40]   ��¼����
                                                                Array.Clear(strsttt, 0, strsttt.Length);
                                                                for (int loc = 0; loc < datlen; loc++)
                                                                {
                                                                    strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                                }

                                                                textBox1.AppendText(System.Text.Encoding.Default.GetString(strsttt));//�Ի���׷����ʾ����       
                                                                textBox1.AppendText("\r\n");
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF100_WRITE_READ1://buf[100]
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_IP_WRITE_READ://IP��ַ
                                                                {
                                                                    textBox9.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 1].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 2].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 3].ToString();

                                                                    textBox1.AppendText("IP��ַ:");//�Ի���׷����ʾ���� 
                                                                    textBox1.AppendText(textBox9.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_MAC_WRITE_READ://MAC��ַ
                                                                {
                                                                    textBox8.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 1].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 2].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 3].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 4].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 5].ToString();

                                                                    textBox1.AppendText("MAC��ַ:");//�Ի���׷����ʾ���� 
                                                                    textBox1.AppendText(textBox8.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_USART_BUND_WRITE_READ://���ڲ�����
                                                                {
                                                                    comboBox3.SelectedIndex = TCPSerialReceiveBuf[TCPSerialBufferOffset];


                                                                    textBox1.AppendText("���ڲ�����:");//�Ի���׷����ʾ����     
                                                                    textBox1.AppendText(comboBox3.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_DEVICE_ADD_WRITE_READ://���ڲ�����
                                                                {
                                                                    numericUpDown3.Value = TCPSerialReceiveBuf[TCPSerialBufferOffset];


                                                                    textBox1.AppendText("�豸��:");//�Ի���׷����ʾ����     
                                                                    textBox1.AppendText(numericUpDown3.Text);//�Ի���׷����ʾ����       
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

                                                                    textBox1.AppendText(textBox15.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U16_WRITE_READ1://U16
                                                                {
                                                                    UInt16 add = (UInt16)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8));


                                                                    textBox14.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox14.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U32_WRITE_READ1://U32
                                                                {
                                                                    UInt32 add = (UInt32)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8) + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 2] << 16) + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 3] << 24));


                                                                    textBox13.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox13.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF20_WRITE_READ1://buf[20]  ���к�
                                                                Array.Clear(strsttt, 0, strsttt.Length);
                                                                for (int loc = 0; loc < datlen; loc++)
                                                                {
                                                                    strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                                }
                                                                textBox10.Text = Encoding.Default.GetString(strsttt);
                                                                //   textBox10.Text = Encoding.GetEncoding("GB2312").GetString(strsttt);


                                                                textBox1.AppendText(textBox10.Text);//�Ի���׷����ʾ����       
                                                                textBox1.AppendText("\r\n");

                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF40_WRITE_READ1://buf[40]   ��¼����
                                                                Array.Clear(strsttt, 0, strsttt.Length);
                                                                for (int loc = 0; loc < datlen; loc++)
                                                                {
                                                                    strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                                }

                                                                textBox1.AppendText(Encoding.Default.GetString(strsttt));//�Ի���׷����ʾ����       
                                                                textBox1.AppendText("\r\n");
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF100_WRITE_READ1://buf[100]
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_IP_WRITE_READ://IP��ַ
                                                                {
                                                                    textBox12.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 1].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 2].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 3].ToString();

                                                                    textBox1.AppendText("IP��ַ:");//�Ի���׷����ʾ���� 
                                                                    textBox1.AppendText(textBox12.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_MAC_WRITE_READ://MAC��ַ
                                                                {
                                                                    textBox11.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 1].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 2].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 3].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 4].ToString() + "." +
                                                                    TCPSerialReceiveBuf[TCPSerialBufferOffset + 5].ToString();

                                                                    textBox1.AppendText("MAC��ַ:");//�Ի���׷����ʾ���� 
                                                                    textBox1.AppendText(textBox11.Text);//�Ի���׷����ʾ����       
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

                                                                    textBox1.AppendText(textBox20.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U16_WRITE_READ1://U16
                                                                {
                                                                    UInt16 add = (UInt16)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8));


                                                                    textBox19.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox19.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_U32_WRITE_READ1://U32
                                                                {
                                                                    UInt32 add = (UInt32)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8) + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 2] << 16) + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 3] << 24));


                                                                    textBox18.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox18.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]
                                                                }
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF20_WRITE_READ1://buf[20]  ���к�
                                                                Array.Clear(strsttt, 0, strsttt.Length);
                                                                for (int loc = 0; loc < datlen; loc++)
                                                                {
                                                                    strsttt[loc] = TCPSerialReceiveBuf[TCPSerialBufferOffset + loc];
                                                                }
                                                                textBox21.Text = Encoding.Default.GetString(strsttt);
                                                                //   textBox10.Text = Encoding.GetEncoding("GB2312").GetString(strsttt);


                                                                textBox1.AppendText(textBox21.Text);//�Ի���׷����ʾ����       
                                                                textBox1.AppendText("\r\n");

                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF40_WRITE_READ1://buf[40]   ��¼����

                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_BUF100_WRITE_READ1://buf[100]
                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_IP_WRITE_READ://IP��ַ

                                                                break;
                                                            case GNBootLoaderCmd_Fun_G.SYSCONFIG_TCP_MAC_WRITE_READ://MAC��ַ

                                                                break;
                                                        }
                                                    }
                                                    break;

                                            }
                                        }
                                        break;
                                    case GNBootLoaderCmd_G.BOOT_SELFCONFIG_WRITE://0x05  ϵͳ����д��
                                    case GNBootLoaderCmd_G.BOOT_SELFCONFIG_READ://0x06  ϵͳ������ȡ
                                        {
                                            switch (TCPSerialReceiveBuf[0])
                                            {//���ݵ�ǰ�豸�ţ���ʾ�ڲ�ͬ������
                                                case 1:
                                                    {
                                                        switch (TCPSerialReceiveBuf[2])
                                                        {
                                                            case 1://version 
                                                                {
                                                                    textBox17.Text = TCPSerialReceiveBuf[TCPSerialBufferOffset].ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox17.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case 2://version2
                                                                {
                                                                    UInt16 add = (UInt16)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8));


                                                                    textBox16.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox16.Text);//�Ի���׷����ʾ����       
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

                                                                    textBox1.AppendText(textBox17.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case 2://version2
                                                                {
                                                                    UInt16 add = (UInt16)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8));


                                                                    textBox16.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox16.Text);//�Ի���׷����ʾ����       
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

                                                                    textBox1.AppendText(textBox23.Text);//�Ի���׷����ʾ����       
                                                                    textBox1.AppendText("\r\n");
                                                                }
                                                                break;
                                                            case 2://version2
                                                                {
                                                                    UInt16 add = (UInt16)(TCPSerialReceiveBuf[TCPSerialBufferOffset] + (TCPSerialReceiveBuf[TCPSerialBufferOffset + 1] << 8));


                                                                    textBox22.Text = add.ToString();
                                                                    // TCPSerialReceiveBuf[TCPSerialBufferOffset]

                                                                    textBox1.AppendText(textBox22.Text);//�Ի���׷����ʾ����       
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



        //��������ָ���
        #region CMD ͨ��ָ�������ָ���ʾ��


        //ϵͳ������ȡ  03 04ָ��
        #region CDM03&CMD04

        /// <summary>
        /// 1# U8����д��
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
        /// 1# U8������ȡ
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
        ///1#  U16����д��
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
        ///1#  U16������ȡ
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
        ///1#  U32����д��
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
        ///1#  U32������ȡ
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
        ///1#  дIP��ַ
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
        ///1#  ��ȡIP��ַ
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
        ///1#  дMAC��ַ
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
        /// 1#   ��ȡMAC��ַ
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
        ///1#  ������д��
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
        ///1#  �����ʶ�ȡ
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
        ///1#  �豸��д��
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
        ///1#  �豸�Ŷ�ȡ
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
        ///1#  д����¼����
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
        ///1#  д���豸���к�
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
                MessageBox.Show("�ַ����Ȳ�����,���Ϊ5-20���ַ�����!", "����");
            }
        }

        #endregion
        //0x02 ָ���ȡ
        #region CDM02

        /// <summary>
        /// ��ȡ����״̬
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
        /// ��ȡ����汾��
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
        /// ��ȡӲ���汾��
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
        /// ��ȡ�̼��汾��
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
        ///1#  ��ȡ��¼����
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
        /// ��ȡ�豸������
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
        ///1#  ��ȡ�豸���к�
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
        //0x01 ָ���
        #region  CMD01


        /// <summary>
        /// �豸����
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
        /// ����BAK
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
        /// ����APP
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
        /// У��Firmware ����ļ�
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
                textBox1.AppendText("���Firmware�ļ�\r\n");//�Ի���׷����ʾ����
            }

        }
        /// <summary>
        /// У��BAK����ļ�
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
                textBox1.AppendText("���APP�ļ�\r\n");//�Ի���׷����ʾ����
            }
        }
        /// <summary>
        /// У��APP����ļ�
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
                textBox1.AppendText("���APP�ļ�\r\n");//�Ի���׷����ʾ����
            }
        }
        /// <summary>
        /// У��BAK��APP
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
        /// ����BAK
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
        /// ��ʼ����
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button11_Click(object sender, EventArgs e)
        {
            if ((Data_ok > 0) && (BinSendFlag == false))
            {//����Ҫ�ļ���������δ��ʼ����

                BinSendFlag = true;//��ʼ����
                BinSendStep = 0;//��������
                SendTIMM = 1000;//����ʱ

                toolStripProgressBar1.Value = 0;
            }
        }


        /// <summary>
        /// ��ת��BOOT
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
        /// ��ת��APP
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

        //��������  
        //����ѡ�񡢴򿪡����͡����ݽ���
        //TCP���ӡ����͡�����
        //ָ��ͷ��
        #region ��������


        /// <summary>
        /// 50���붨ʱ��ʱ
        /// ��Ҫ���ڳ�ʱ�ж�
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1_Tick(object sender, EventArgs e)
        {

            //�������ݺ󣬿�ʼ����ʱ
            //����ʱ������δ��Ӧ���� ���ж���ʱ
            if (DataCountDown > 49)
            {
                DataCountDown -= 50;
            }

            //��ʼ�����ļ���ʼ��ʱ
            if (BinSendFlag)
            {
                BinSendTim += 50;
            }

            //��������ָ�����Ҫ�ڽ��յ���Ӧ�������ظ�����
            //����˵�����������ļ�ʱ����Ҫ��������
            if (SendTIMM > 49)
            {
                SendTIMM -= 50;
            }

            toolStripStatusLabel2.Text = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");
        }


        /// <summary>
        /// �����ʾ������
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button12_Click(object sender, EventArgs e)
        {
            textBox1.Clear();
        }
        /// <summary>
        /// TCP����
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button6_Click(object sender, EventArgs e)
        {
            OpenCloseTcpServer();
        }
        /// <summary>
        /// �������ô���
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox1_Click(object sender, EventArgs e)
        {
            SearchSerialPort();
        }

        /// <summary>
        /// ����5  ��/�رմ���
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button5_Click(object sender, EventArgs e)
        {
            OpenCloseSerial();
        }

        /// <summary>
        /// ��APP�ļ�
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button10_Click(object sender, EventArgs e)
        {
            fileDialog.Multiselect = true;
            fileDialog.Title = "��ѡAPP���ļ�";
            fileDialog.Filter = "bin�ļ�|*.bin";

            if (fs != null)
            {
                fs.Close();
            }

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                fs = new FileStream(fileDialog.FileName, FileMode.Open);


                if (fs.Length > 2 * 1024)
                {//�ļ���С�������2k
                    Data_ok = 1;

                    Length_bin = fs.Length;//�ļ�����
                    Array.Clear(Data_bin, 0, Data_bin.Length);//������ϴεĻ�������
                    fs.Read(Data_bin, 0, (int)fs.Length);

                    textBox1.AppendText("���ļ�\r\n");//�Ի���׷����ʾ����
                    textBox1.AppendText(fs.Name);//�Ի���׷����ʾ����
                    textBox1.AppendText("\r\n�ļ���С:");//�Ի���׷����ʾ����
                    textBox1.AppendText((fs.Length / 1024.0).ToString());//�Ի���׷����ʾ����
                    textBox1.AppendText("kb\r\n");//�Ի���׷����ʾ����
                                                  //   label16.Text = (fs.Length / 1024.0).ToString() + "kb\r\n" + fs.Name ;
                                                  //Array.Copy(byteRead, Data_bin, byteRead.Length);
                                                  //Long_bin = byteRead.Length;

                    toolStripStatusLabel3.Text = "APP�ļ�:" + fs.Name;
                }
                else
                {
                    textBox1.AppendText("���ļ�\r\n");//�Ի���׷����ʾ����
                    textBox1.AppendText(fs.Name);//�Ի���׷����ʾ����
                    textBox1.AppendText("\r\n�ļ���С:");//�Ի���׷����ʾ����
                    textBox1.AppendText((fs.Length / 1024.0).ToString());//�Ի���׷����ʾ����
                    textBox1.AppendText("kb\r\n");//�Ի���׷����ʾ����


                    textBox1.AppendText("���ļ��쳣\r\n");//�Ի���׷����ʾ����

                    toolStripStatusLabel3.Text = "APP�ļ���ʧ��";



                }
            }
        }
        /// <summary>
        /// �򿪹̼��ļ�
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button30_Click(object sender, EventArgs e)
        {
            fileDialog.Multiselect = true;
            fileDialog.Title = "��ѡ��Firmware�ļ�";
            fileDialog.Filter = "bin�ļ�|*.bin";

            if (fs != null)
            {
                fs.Close();
            }

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                fs = new FileStream(fileDialog.FileName, FileMode.Open);


                if (fs.Length < 128 * 1024)
                {//�ļ���С�������2k
                    Data_ok = 2;

                    Length_bin = fs.Length;//�ļ�����
                    Array.Clear(Data_bin, 0, Data_bin.Length);//������ϴεĻ�������
                    fs.Read(Data_bin, 0, (int)fs.Length);

                    textBox1.AppendText("��Firmware�ļ�\r\n");//�Ի���׷����ʾ����
                    textBox1.AppendText(fs.Name);//�Ի���׷����ʾ����
                    textBox1.AppendText("\r\n�ļ���С:");//�Ի���׷����ʾ����
                    textBox1.AppendText((fs.Length / 1024.0).ToString());//�Ի���׷����ʾ����
                    textBox1.AppendText("kb\r\n");//�Ի���׷����ʾ����

                    toolStripStatusLabel3.Text = "Firmware�ļ�:" + fs.Name;

                }
                else
                {
                    textBox1.AppendText("��Firmware�ļ�\r\n");//�Ի���׷����ʾ����
                    textBox1.AppendText(fs.Name);//�Ի���׷����ʾ����
                    textBox1.AppendText("\r\n�ļ���С:");//�Ի���׷����ʾ����
                    textBox1.AppendText((fs.Length / 1024.0).ToString());//�Ի���׷����ʾ����
                    textBox1.AppendText("kb\r\n");//�Ի���׷����ʾ����


                    textBox1.AppendText("���ļ��쳣\r\n");//�Ի���׷����ʾ����


                    toolStripStatusLabel3.Text = "Firmware�ļ���ʧ��";

                }
            }
        }

        /// <summary>
        /// �豸���иı�ʱ�����и���
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
            { //ʹ��17���豸
                tabPage3.Parent = tabControl2;//��ʾϵͳ����
                tabPage5.Parent = tabControl2;//��ʾ17���豸�����ý���
                tabPage6.Parent = null;//����ʾ18���豸�����ý���
            }
            else if (comboBox4.SelectedIndex == 1)
            { //ʹ��18���豸
                tabPage3.Parent = tabControl2;//��ʾϵͳ����
                tabPage5.Parent = null;//����ʾ17���豸�����ý���
                tabPage6.Parent = tabControl2;//��ʾ18���豸�����ý���

            }
            else if (comboBox4.SelectedIndex == 2)
            { //ʹ��19���豸
                tabPage3.Parent = null;//����ʾϵͳ����
                tabPage5.Parent = null;//����ʾ17���豸�����ý���
                tabPage6.Parent = null;//����ʾ18���豸�����ý���
            }
        }


        enum GNBootLoaderCmd_G
        {
            //ָ�����
            BOOT_CDM_WRITE = 1,     // ������ص�д
            BOOT_CDM_READ,     // ������صĶ�
            BOOT_SYSCONFIG_WRITE,//ϵͳ����д��
            BOOT_SYSCONFIG_READ,//ϵͳ������ȡ

            BOOT_SELFCONFIG_WRITE,//�豸����д
            BOOT_SELFCONFIG_READ,//�豸���ƶ�
            BOOT_CDM_CNT,



        }

        /// <summary>
        /// ��ǰ��λ��֧�ֵ�ָ������
        /// </summary>
        enum GNBootLoaderCmd_Fun_G
        {
            //������ص�дָ��  01
            BOOT_WRITE_Rst = 1,     // ��λ
            BOOT_WRITE_EraseChip,   // ����
            BOOT_WRITE_Jump_Back,   // ��תApp/BOOT
            BOOT_WRITE_Download,    // �����ļ��������
            BOOT_WRITE_CheckComp,   // У�����
            BOOT_WRITE_StartLoad,   // �����³���
            //BOOT_WRITE_ProgramDate, // д����¼����
            //BOOT_WRITE_EquipmentSerial, // д���豸���к�
            BOOT_WRITE_CNT,
            //������صĶ�ָ��  02
            BOOT_READ_Runstatus = 1, // ��ȡ����״̬
            BOOT_READ_SoftVerion,    // ��ȡ����汾��
            BOOT_READ_HardVerion,    // ��ȡӲ���汾��
            BOOT_READ_FirmwareVerion, // ��ȡ�̼��汾��
                                      //      BOOT_READ_ProgramDate,   // ��ȡ��¼����
            BOOT_READ_DeviceDes,     // ��ȡ�豸������
            //BOOT_READ_EquipmentSerial,     // ��ȡ�豸���к�
            BOOT_READ_CNT,

            //ϵͳ�����Ķ�д  03  04
            SYSCONFIG_U8_WRITE_READ1 = 1,//U8������ַ
            SYSCONFIG_U8_WRITE_READ2,
            SYSCONFIG_U8_WRITE_READ3,
            SYSCONFIG_U16_WRITE_READ1 = 11,//U16������ַ
            SYSCONFIG_U32_WRITE_READ1 = 21,//U32������ַ
            SYSCONFIG_BUF20_WRITE_READ1 = 31,//buf[20]������ַ
            SYSCONFIG_BUF20_WRITE_READ2,
            SYSCONFIG_BUF40_WRITE_READ1 = 41,//buf[40]������ַ
            SYSCONFIG_BUF40_WRITE_READ2,
            SYSCONFIG_BUF100_WRITE_READ1 = 51,//buf[100]������ַ
            SYSCONFIG_BUF100_WRITE_READ2,
            SYSCONFIG_BUF512_WRITE_READ1 = 61,//buf[512]������ַ
            SYSCONFIG_BUF512_WRITE_READ2,
            SYSCONFIG_BUF1024_WRITE_READ1 = 71,//buf[1024]������ַ
            SYSCONFIG_BUF1024_WRITE_READ2,
            SYSCONFIG_BUF2048_WRITE_READ1 = 81,//buf[2048]������ַ
            SYSCONFIG_BUF2048_WRITE_READ2,
            SYSCONFIG_TCP_IP_WRITE_READ = 171,//IP��ַ
            SYSCONFIG_TCP_MAC_WRITE_READ,//MAC��ַ
            SYSCONFIG_USART_BUND_WRITE_READ = 191,//���ڲ�����
            SYSCONFIG_DEVICE_ADD_WRITE_READ = 201,//�豸��  Ԥ��


        }

        /// <summary>
        /// ָ���ģ��
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
            {//�豸δ����
                return;
            }

            byte[] databuf = new byte[4096];
            int datacount = 0;
            //�豸��
            databuf[datacount++] = add;

            databuf[datacount++] = (byte)cmd0;//����
            databuf[datacount++] = (byte)cmd1;//��������

            databuf[datacount++] = (byte)(len >> 8);//���ݳ���
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

            DataCountDown = 2000;//2��δ��Ӧ�ж���ʱ  50ms�Ķ�ʱ�ж�ʵʱ���

            DataCountDownFly = true;//ָ���ѷ��ͱ�־λ
        }


        /// <summary>
        /// У�麯��
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
        /// ���ݷ���
        /// </summary>
        /// <param name="data"></param>
        /// <param name="num"></param>
        /// <param name="connectdevice"></param>
        private void SendBuf(byte[] data, int num, byte connectdevice)
        {
            if (connectdevice == 1)
            {//���ڷ���
                SendDataSerial(data, num);
            }
            else if (connectdevice == 2)
            {
                TcpSendData(data, num);
            }
        }

        /// <summary>
        /// ������ǰ���ô���
        /// </summary>
        private void SearchSerialPort()
        {
            comboBox1.Items.Clear();

            //��ȡ���Ե�ǰ���ô��ڲ���ӵ�ѡ���б���
            comboBox1.Items.AddRange(SerialPort.GetPortNames());

            try
            {
                comboBox1.SelectedIndex = 0;
            }
            catch
            {
                MessageBox.Show("û�п��ô���", "������ʾ");
            }
        }
        /// <summary>
        /// ���ڴ�/�ر�
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
                    serialPort1.StopBits = StopBits.One;   //ֹͣλ;
                    serialPort1.DataReceived += new SerialDataReceivedEventHandler(sp_DataReceived);
                    serialPort1.Open();


                }
            }
            catch { }


            if (serialPort1.IsOpen)
            {
                comboBox1.Enabled = false;
                comboBox2.Enabled = false;

                button5.Text = "�ر�";

                connect_device = 1;
                //���ڴ򿪺󣬲������ٲ���TCP
                tabPage2.Parent = null;//���� TCP���ý���
                //       tabPage2.Visible = false;

                groupBox1.Enabled = true;//��ʾ�豸��ѡ��


                //����״̬��ʾ
                toolStripStatusLabel1.Text = "����������";

                textBox1.AppendText("�豸�Ѿ���������\r\n");//�Ի���׷����ʾ����
                textBox1.AppendText("��ǰ�Ƕ��豸bootloader����ϵͳ\r\n");//�Ի���׷����ʾ����
            }
            else
            {
                comboBox1.Enabled = true;
                comboBox2.Enabled = true;

                button5.Text = "��";

                connect_device = 0;
                //���ڹرպ󣬿�������ѡ��TCP
                tabPage2.Parent = tabControl1;//��ʾ,TCP���ý���

                groupBox1.Enabled = false;//�����豸��ѡ��
                tabPage3.Parent = null;//����ʾϵͳ����
                tabPage5.Parent = null;//����ʾ17���豸�����ý���
                tabPage6.Parent = null;//����ʾ18���豸�����ý���

                comboBox4.Text = null;
                //   tabPage2.Visible = true;


                //����״̬��ʾ
                toolStripStatusLabel1.Text = "�豸δ����";



            }
        }
        /// <summary>
        /// �������ݽ���
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (!serialPort1.IsOpen)//�����ڹر�ʱ���������� Ϊ�˷�ֹ�رմ���ʱ����������
                {
                    serialPort1?.DiscardInBuffer();//�������ջ���������
                    return;
                }
                if (serialPort1.BytesToRead < 4)
                {
                    return;
                }

                Thread.Sleep(25);//��ʱ25ms �ȴ�����������

                TCPSerialRecveLength = serialPort1.BytesToRead;//�������ݵ��ֽ���
                serialPort1.Read(TCPSerialReceiveBuf, 0, TCPSerialRecveLength); //��ȡ�����յ������� 

                TCPSerialReceiveFlag = true;
            }
            catch
            {

            }
        }
        /// <summary>
        /// ���ڷ�������
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
        /// ��/�ر�TCP����
        /// </summary>
        private void OpenCloseTcpServer()
        {
            RemoteIp = textBox4.Text;//��ȡIP��ַ
            RemotePort = Convert.ToInt32(textBox3.Text);          //��ȡ�˿ں�

            if (button6.Text == "����")
            {
                //IP��ַ �� �˿ں����벻Ϊ��
                if (string.IsNullOrEmpty(textBox3.Text) == false && string.IsNullOrEmpty(textBox4.Text) == false)
                {
                    if (ConnectToServer(true))
                    {

                    }
                }
                else
                {
                    MessageBox.Show("IP��ַ��˿ںŴ���!", "��ʾ");
                }
            }
            else
            {
                if (ConnectToServer(false))
                {

                }
            }

            Thread.Sleep(100);//��ʱ200ms �ȴ�����״̬

            if (IsConnected)
            {

                button6.Text = "�Ͽ�";
                textBox3.Enabled = false;
                textBox4.Enabled = false;
                connect_device = 2;
                //TCP���ӳɹ�  �������ٲ�������
                tabPage1.Parent = null;//��ʾ,�������ý���

                groupBox1.Enabled = true;//�����豸��ѡ��

                //����״̬��ʾ
                toolStripStatusLabel1.Text = "TCP������";
            }
            else
            {

                button6.Text = "����";
                textBox3.Enabled = true;
                textBox4.Enabled = true;
                connect_device = 0;
                //TCP���ӳɹ�  �������ٲ�������
                tabPage1.Parent = tabControl1;//��ʾ,�������ý���

                groupBox1.Enabled = false;//�����豸��ѡ��
                tabPage3.Parent = null;//����ʾϵͳ����
                tabPage5.Parent = null;//����ʾ17���豸�����ý���
                tabPage6.Parent = null;//����ʾ18���豸�����ý���

                comboBox4.Text = null;


                //����״̬��ʾ
                toolStripStatusLabel1.Text = "�豸δ����";
            }
        }
        /// <summary>
        /// ��TCP����
        /// </summary>
        private static bool ConnectToServer(bool ok)
        {
            try
            {
                //��ʼ����
                if (ok == true)
                {
                    //��ʼ��TCP�ͻ��˶���
                    tcpClient = new TcpClient();  //Tcp�ͻ���ģ��
                    tcpClient.BeginConnect(RemoteIp, RemotePort, new AsyncCallback(AsynConnect), tcpClient);
                }
                else
                {
                    tcpClient.GetStream().Close();
                    tcpClient.Close();
                    //�ر����Ӻ����ϸ�������״̬��־
                    IsConnected = false;
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("TcpClientBusiness", "ConnectToServer|�쳣��Ϣ��" + ex.Message.ToString());
            }
            return false;
        }

        /// <summary>
        /// �첽����
        /// </summary>
        /// <param name="iar"></param>
        private static void AsynConnect(IAsyncResult iar)
        {
            try
            {
                //���ӳɹ�
                tcpClient.EndConnect(iar);
                //���ӳɹ���־
                IsConnected = true;
                networkStream = tcpClient.GetStream();
                byte[] TempBytes = new byte[ReceiveDataBufLength];
                //��ʼ�첽��ȡ��������
                networkStream.BeginRead(TempBytes, 0, TempBytes.Length, new AsyncCallback(AsynReceiveData), TempBytes);
            }
            catch (Exception ex)
            {
                //    MessageBox.Show("TcpClientBusiness", "AsynConnect|�쳣��Ϣ��" + ex.Message.ToString());
            }
        }
        /// <summary>
        /// ��������
        /// <param name="SendBytes">��Ҫ���͵�����</param>
        /// </summary>
        private static void TcpSendData(byte[] SendBytes, int num)
        {
            try
            {
                if (networkStream.CanWrite && SendBytes != null && num > 0)
                {
                    //��������
                    networkStream.Write(SendBytes, 0, num);
                    networkStream.Flush();
                }
            }
            catch (Exception ex)
            {
                if (tcpClient != null)
                {
                    tcpClient.Close();
                    //�ر����Ӻ����ϸ�������״̬��־
                    IsConnected = false;
                }
                //     MessageBox.Show("TcpClientBusiness", "SendData|�쳣��Ϣ��" + ex.Message.ToString());
            }
        }

        /// <summary>
        /// �첽��������
        /// </summary>
        /// <param name="iar"></param>
        private static void AsynReceiveData(IAsyncResult iar)
        {
            byte[] CurrentBytes = (byte[])iar.AsyncState;
            try
            {
                //�����˱������ݽ���
                TCPSerialRecveLength = networkStream.EndRead(iar);
                //int num = networkStream.EndRead(iar);
                //����չʾ���ΪInfoModel��CurrBytes���ԣ������ص������������������������

                TCPSerialReceiveBuf = CurrentBytes;
                //ResponseBytes.Add(CurrentBytes);

                TCPSerialReceiveFlag = true;

                //���������������������첽��ȡ��Ŀǰ��ÿ�����յ��ֽ����ݳ��Ȳ��ᳬ��1024��
                byte[] NewBytes = new byte[ReceiveDataBufLength];
                networkStream.BeginRead(NewBytes, 0, NewBytes.Length, new AsyncCallback(AsynReceiveData), NewBytes);

            }
            catch (Exception ex)
            {
                //    MessageBox.Show("TcpClientBusiness", "AsynReceiveData|�쳣��Ϣ��" + ex.Message.ToString());
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

        #region 17# ����ԭ��F407

        /// <summary>
        /// 17#   version��ȡ
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
        /// 17#   versionд��
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
        /// 17#   version2��ȡ
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
        /// 17#   version2д��
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
        /// 17# ��ȡU8
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
        /// 17#  д��U8
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
        /// 17#  ��ȡU16
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
        /// 17#  д��U16
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
        /// 17#  ��ȡu32
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
        /// 17#  д��U32
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
        /// 17#  ��ȡ��¼����
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
        /// 17#  д����¼����
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
        /// 17#  ��ȡIP��ַ
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
        /// 17#  д��IP��ַ
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
        /// 17#  ��ȡMAC��ַ
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
        ///17#  д��MAC��ַ
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
        /// 17#  ��ȡ�豸���к�
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
        /// 17#  д���豸���к�
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
                MessageBox.Show("�ַ����Ȳ�����,���Ϊ5-20���ַ�����!", "����");
            }
        }

        #endregion


        #region 18# STM32��Ŀ����ѧϰ

        /// <summary>
        /// 18# �豸���кŶ�ȡ
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
        /// 18# �豸���к�д��
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
                MessageBox.Show("�ַ����Ȳ�����,���Ϊ5-20���ַ�����!", "����");
            }
        }
        /// <summary>
        /// 18#  ��ȡversion
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
        /// 18#  д��version
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
        /// 18#  ��ȡversion2
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
        /// 18#  ��ȡversion2
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
        /// 18# ��ȡU8
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
        /// 18#  д��U8
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
        /// 18# ��ȡU16
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
        /// 18#  д��U16
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
        /// 18# ��ȡU32
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
        /// 18#  д��U32
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