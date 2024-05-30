using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
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
        private const string DATABASE_LOCATION = @"C:\Users\MMC\Desktop\ObjectOProgramming\SecurityProject\ThreadsComCliente\Database.mdf";
        private const int SALTSIZE = 8;
        private const int NUMBER_OF_ITERATIONS = 1000;

        private const int PORT = 10000;
        NetworkStream networkStream;
        ProtocolSI protocolSI;
        TcpClient client;
        AesCryptoServiceProvider aes;
        RSACryptoServiceProvider rsa;
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

                aes = new AesCryptoServiceProvider();

                //O CLIENTE ENVIA AO SERVIDOR A SUA CHAVE PUBLICA
                //LOGO temos de crirar a chave publica

                //Algoritmo assimetrico
                rsa = new RSACryptoServiceProvider();

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

                //decifrar chaveSym utilizando o RSA (chave publica do cliente)
                aes.Key = rsa.Decrypt(Convert.FromBase64String(chaveSym), true); //ERRO POR PARTE DA INSTANCIA DO RSA NAO SER A MESMA QUE A DO SERVIDOR????



                //LEITURA DO IV CRIADO NO LADO DO SERVIDOR COM BASE NA CHAVE PUBLICA DO CLIENTE
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                string IV = protocolSI.GetStringFromData();

                aes.IV = rsa.Decrypt(Convert.FromBase64String(IV), true);

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

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (textBoxUsername.Text != null || textBoxPassword.Text != null)
            {
                if (VerifyLogin(textBoxUsername.Text, textBoxPassword.Text))
                {
                    MessageBox.Show("LOGIN REALIZADO COM SUCESSO");
                    groupBoxConversa.Enabled = true;
                    groupBoxLogin.Enabled = false;
                }
            }
            else
                MessageBox.Show("Preencha todos os parâmetros");
        }

        private void btnRegisto_Click(object sender, EventArgs e)
        {
            byte[] novoSalt = GenerateSalt(SALTSIZE);

            byte[] saltedHashedPass = GenerateSaltedHash(textBoxPassword.Text, novoSalt);

            Register(textBoxUsername.Text, saltedHashedPass, novoSalt);
        }

        private bool VerifyLogin(string username, string password)
        {
            SqlConnection conn = null;
            try
            {
                // Configurar ligação à Base de Dados
                conn = new SqlConnection();
                conn.ConnectionString = String.Format(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=" + DATABASE_LOCATION + ";Integrated Security=True");

                // Abrir ligação à Base de Dados
                conn.Open();

                // Declaração do comando SQL
                String sql = "SELECT * FROM Users WHERE Username = @username";
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = sql;

                // Declaração dos parâmetros do comando SQL
                SqlParameter param = new SqlParameter("@username", username);

                // Introduzir valor ao parâmentro registado no comando SQL
                cmd.Parameters.Add(param);

                // Associar ligação à Base de Dados ao comando a ser executado
                cmd.Connection = conn;

                // Executar comando SQL
                SqlDataReader reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                {
                    throw new Exception("Error while trying to access an user");
                }

                // Ler resultado da pesquisa
                reader.Read();

                // Obter Hash (password + salt)
                byte[] saltedPasswordHashStored = (byte[])reader["SaltedPasswordHash"];

                // Obter salt
                byte[] saltStored = (byte[])reader["Salt"];

                conn.Close();

                //verificar o hash que temos na base de dados com a pass hased
                // que foi introduzida no login

                byte[] hashComparar = GenerateSaltedHash(password, saltStored);

                //Funcao que compara e devolve um bool se as hash sao iguais
                return saltedPasswordHashStored.SequenceEqual(hashComparar);


            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred: " + e.Message);
                return false;
            }
        }

        private void Register(string username, byte[] saltedPasswordHash, byte[] salt)
        {
            SqlConnection conn = null;
            try
            {
                // Configurar ligação à Base de Dados
                conn = new SqlConnection();
                conn.ConnectionString = String.Format(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=" + DATABASE_LOCATION + ";Integrated Security=True");

                // Abrir ligação à Base de Dados
                conn.Open();

                // Declaração dos parâmetros do comando SQL
                SqlParameter paramUsername = new SqlParameter("@username", username);
                SqlParameter paramPassHash = new SqlParameter("@saltedPasswordHash", saltedPasswordHash);
                SqlParameter paramSalt = new SqlParameter("@salt", salt);

                // Declaração do comando SQL
                String sql = "INSERT INTO Users (Username, SaltedPasswordHash, Salt) VALUES (@username,@saltedPasswordHash,@salt)";

                // Prepara comando SQL para ser executado na Base de Dados
                SqlCommand cmd = new SqlCommand(sql, conn);

                // Introduzir valores aos parâmentros registados no comando SQL
                cmd.Parameters.Add(paramUsername);
                cmd.Parameters.Add(paramPassHash);
                cmd.Parameters.Add(paramSalt);

                // Executar comando SQL
                int lines = cmd.ExecuteNonQuery();

                // Fechar ligação
                conn.Close();

                MessageBox.Show("Registo realizado com sucesso");

                if (lines == 0)
                {
                    // Se forem devolvidas 0 linhas alteradas então o SQL não foi executado com sucesso
                    throw new Exception("Error while inserting an user");
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error while inserting an user:" + e.Message);
            }
        }

        private static byte[] GenerateSalt(int size)
        {
            //Generate a cryptographic random number.
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] buff = new byte[size];
            rng.GetBytes(buff);
            return buff;
        }

        private static byte[] GenerateSaltedHash(string plainText, byte[] salt)
        {
            Rfc2898DeriveBytes rfc2898 = new Rfc2898DeriveBytes(plainText, salt, NUMBER_OF_ITERATIONS);
            return rfc2898.GetBytes(32);
        }

        private void btnEnviar_Click(object sender, EventArgs e)
        {
            //Recolha da msg do client para uma variavel e limpeza da caixa de texto
            string message = textBoxMessage.Text;
            textBoxMessage.Clear();

            string msgCifrada = CifrarTexto(message);

            //Preparacao para o envio da msg
            byte[] packet = protocolSI.Make(ProtocolSICmdType.DATA, msgCifrada);
            networkStream.Write(packet, 0, packet.Length);

            //Até receber um ACK lemos o que esta no buffer, depois fechamos a leitura ate novo ACK
            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
            {
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            }
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
        
        private void FormCliente_FormClosing(object sender, FormClosingEventArgs e)
        {
            byte[] eot = protocolSI.Make(ProtocolSICmdType.EOT);
            networkStream.Write(eot, 0, eot.Length);
            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

            networkStream.Close();
            client.Close();
        }
    }
}





















