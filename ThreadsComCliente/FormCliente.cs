using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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
        AesCryptoServiceProvider aes;
        public FormCliente()
        {
            InitializeComponent();

            //Estabelecer ligacao com o servidor
            try
            {

                //Estabelecer ligacao com o servidor, ao inicializar o form
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
                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();

                //criar uma string XML contendo a chave do objeto AssymetricAlgorithm
                //para obter a chave publica

                string publicKey = rsa.ToXmlString(false); //FALSE devolve UNICAMENTE a Public Key

                //Preparacao para o envio da chave publica
                byte[] packet = protocolSI.Make(ProtocolSICmdType.PUBLIC_KEY, publicKey);

                //envio da chave publica
                networkStream.Write(packet, 0, packet.Length);


                //LEITURA DA CHAVE SIMETRICA CRIADA PELO SERVIDOR COM BASE NA CHAVE PUBLICA DO CLIENTE
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                string chaveSym = protocolSI.GetStringFromData();


                //LEITURA DO IV CRIADO NO LADO DO SERVIDOR COM BASE NA CHAVE PUBLICA DO CLIENTE
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                string IV = protocolSI.GetStringFromData();

            }
            catch (Exception)
            {
                //pop up que deu erro algo do genero
                networkStream.Close();
                client.Close();
            }
            /* usar para fechar a ligacao
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
            }*/
        }

        private void btnEnviar_Click(object sender, EventArgs e)
        {
            ///<summary>Recolha da msg do client para uma variavel e limpeza da caixa de texto</summary>
            string message = textBoxMessage.Text;
            textBoxMessage.Clear();

            ///<summary> Preparacao para o envio da msg</summary>
            byte[] packet = protocolSI.Make(ProtocolSICmdType.DATA, message);
            networkStream.Write(packet, 0, packet.Length);

            ///<summary>Até receber um ACK lemos o que esta no buffer, depois fechamos a leitura ate novo ACK</summary>
            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
            {
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            }
        }

        private void FormCliente_FormClosing(object sender, FormClosingEventArgs e)
        {
            byte[] eot = protocolSI.Make(ProtocolSICmdType.EOT);
            networkStream.Write(eot, 0, eot.Length);
            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

            networkStream.Close();
            client.Close();
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            string username = textBoxUsername.Text;
            string password = textBoxPassword.Text;
            groupBoxConversa.Enabled = true;
            groupBoxLogin.Enabled = false;

        }

        private void btnRegisto_Click(object sender, EventArgs e)
        {
            
        }

        private string CifrarTexto(string textoRaw)
        {
            //conversao do textoRaw em bytes de BASE64
            byte[] textoRaw64 = Encoding.UTF8.GetBytes(textoRaw);

            //variavel para guardar o texto cifrado em bytes
            byte[] textoCifrado;

            //Reservar espaço na memoria para guardar o texto e cifra lo
            MemoryStream memoryStream = new MemoryStream();

            //Inicializar o sitema de cifragem (write)
            CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write);

            //Cifrar os dados
            cryptoStream.Write(textoRaw64, 0, textoRaw64.Length);
            cryptoStream.Close();

            //Guardar os dados cifrados que estao na memoria
            textoCifrado = memoryStream.ToArray();

            //Converter de bytes para base64(texto)
            string textoCifradoBase64 = Convert.ToBase64String(textoCifrado);

            return textoCifradoBase64;
        }

        private string DecifrarTexto(string textoCifradoBase64)
        {
            //conversao da string textoCifrado em array de bytes
            byte[] textoCifrado = Convert.FromBase64String(textoCifradoBase64);

            //Reservar espaço na memoria para guardar o texto e decifra lo
            MemoryStream memoryStream = new MemoryStream(textoCifrado);

            //Inicializar o sitema de DECIFRAGEM (read)
            CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Read);

            //Array de bytes do mesmo tamanho do espaço que reservamos anteriormente
            byte[] bytesParaDecifrar = new byte[memoryStream.Length];

            //varivavel para guardar o numero de bytes decifrados
            int bytesLidos = 0;

            //Decifragem dos Bytes e guardamos a bytes que lemos
            bytesLidos = cryptoStream.Read(bytesParaDecifrar, 0, bytesParaDecifrar.Length);
            cryptoStream.Close();

            //conversao de int para string (texto)
            string textoDecifrado = Encoding.UTF8.GetString(bytesParaDecifrar, 0, bytesLidos);

            return textoDecifrado;
        }
    }
}





















