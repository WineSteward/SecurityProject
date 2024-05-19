using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EI.SI;

namespace ThreadsComCliente
{
    public partial class FormCliente : Form
    {
        private const int PORT = 10000;
        NetworkStream networkStream;
        ProtocolSI protocolSI;
        TcpClient client;
        
        
        public FormCliente()
        {
            InitializeComponent();

            ///<summary>Estabelecer ligacao com o servidor, ao inicializar o form</summary
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, PORT);
            
            client = new TcpClient();
            client.Connect(endPoint);
          
            networkStream = client.GetStream();
            
            protocolSI = new ProtocolSI();

        }

        private void btnEnviar_Click(object sender, EventArgs e)
        {
            ///<summary>Recolha da msg do client para uma variavel e limpeza da caixa de texto</summary>
            string message = textBoxMessage.Text;
            textBoxMessage.Clear();

                ///<summary> Preparacao para o envio da msg</summary>
            byte[] packet = protocolSI.Make(ProtocolSICmdType.DATA, message);
            networkStream.Write(packet, 0, packet.Length);

            ///<summary>Apos receber um ACK lemos o que esta no buffer, depois fechamos a leitura ate novo ACK</summary>
            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
            {
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            }
        }

        private void btnSair_Click(object sender, EventArgs e)
        {

            byte[] eot = protocolSI.Make(ProtocolSICmdType.EOT);
            networkStream.Write(eot, 0, eot.Length);
            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

            networkStream.Close();
            client.Close();
        
        }
    }
}





















