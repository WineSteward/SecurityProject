using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EI.SI;

namespace LoginScreen
{
    public partial class FormLogin : Form
    {

        private const int PORT = 10000;
        NetworkStream networkStream;
        ProtocolSI protocolSI;
        TcpClient client;


        public FormLogin()
        {
            InitializeComponent();
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {

            try
            {

                ///<summary>Estabelecer ligacao com o servidor, ao inicializar o form</summary
                //criar um conjunto IP + Port do servidor
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, PORT);

                //instaciar o cliente TCP
                client = new TcpClient();

                //efetuar a ligacao ao servidor
                client.Connect(endPoint);

                //obter a ligacao do servidor
                networkStream = client.GetStream();

                protocolSI = new ProtocolSI();

                //O CLIENTE ENVIA AO SERVIDOR A SUA CHAVE PUBLICA
                //LOGO temos de crirar a chave publica

                //Algoritmo assimetrico
                //DEFINIR E INSTANCIAR O RSA
                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();

                //criar uma string XML contendo a chave do objeto AssymetricAlgorithm
                //para obter a chave publica

                string publicKey = rsa.ToXmlString(false); //FALSE devolve UNICAMENTE a Public Key

                ///<summary> Preparacao para o envio da msg</summary>
                byte[] packet = protocolSI.Make(ProtocolSICmdType.PUBLIC_KEY, publicKey);
                networkStream.Write(packet, 0, packet.Length);

            }
            catch (Exception)
            {
                //pop up que deu erro algo do genero
            }

            finally
            {
                //Fechar a ligacao se estiver aberta
                if (networkStream != null)
                {
                    networkStream.Close();
                }
                //Fecha a comunicacao se estiver aberta
                if (client != null)
                {
                    client.Close();

                }
            }
            
        }

        private void btnRegisto_Click(object sender, EventArgs e)
        {

        }
    }
}
























