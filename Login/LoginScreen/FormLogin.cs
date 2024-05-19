using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
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
            ///<summary>Estabelecer ligacao com o servidor, ao inicializar o form</summary
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, PORT);

            client = new TcpClient();
            client.Connect(endPoint);

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

            string bothKeys = rsa.ToXmlString(true); // TRUE devolve AMBAS AS CHAVES
            
        }

        private void btnRegisto_Click(object sender, EventArgs e)
        {

        }
    }
}
























