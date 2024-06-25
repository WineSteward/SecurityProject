using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
        byte[] ack;

        //chaves assimétricas
        private string publicKey;
        private string privateKey;

        //chave simétrica
        private string chaveSecreta;
        //vetor de inicialização
        private string iv;

        public FormCliente()
        {
            InitializeComponent();
            textBoxPassword.PasswordChar = '*';

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

                aes = new AesCryptoServiceProvider();

                //obter a ligacao do servidor
                networkStream = client.GetStream();

                protocolSI = new ProtocolSI();

                // cria as chave publica e privada do cliente
                Gerar_chaves();
                //envio da chave publica para o servidor
                Enviar_Chave_Publica();

                //inicia a thread do cliente para ficar a escuta dos dados que o servidor devolve               
                Thread thread = new Thread(threadClient);
                thread.Start();

            }
            catch (Exception)
            {
                //pop up que deu erro algo do genero
                networkStream.Close();
                client.Close();
            }
        }

        private void threadClient()
        {
            protocolSI = new ProtocolSI();

            //Enquanto nao receber a informacao para fechar a comunicacao nao termina
            while (protocolSI.GetCmdType() != ProtocolSICmdType.EOT)
            {

                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

                //Com este switch vamos determinar o tipo de msg que estamos a receber
                switch (protocolSI.GetCmdType())
                {
                     
                    case ProtocolSICmdType.USER_OPTION_1:

                        Chave_simetrica(protocolSI);

                        break;

                    case ProtocolSICmdType.USER_OPTION_2:

                        Vetor_inicializacao(protocolSI);

                        break;

                    //Recebi uma msg (string)
                    case ProtocolSICmdType.DATA:

                        //decifragem da mensagem recebida do servidor
                        string mensagemDecifrada = DecifrarTexto(protocolSI.GetStringFromData());


                        textBoxConversa.Invoke((MethodInvoker)delegate
                        {
                            // Running on the UI thread
                            textBoxConversa.AppendText(mensagemDecifrada+ "\r\n");
                        });


                        //criamos um ack
                        ack = protocolSI.Make(ProtocolSICmdType.ACK);

                        //devolvemos o ack para a stream
                        networkStream.Write(ack, 0, ack.Length);

                        break;

                    //receber mensagem do servidor providenciado pelo outro cliente
                    case ProtocolSICmdType.USER_OPTION_9:

                        string msg = protocolSI.GetStringFromData();

                        //string msg = DecifrarTexto(msgCifrada);

                        textBoxConversa.Invoke((MethodInvoker)delegate
                        {
                            // Running on the UI thread
                            textBoxConversa.AppendText(msg+ "\r\n");
                        });

                        break;
                }
            }

            networkStream.Close();
            client.Close();

            this.Invoke((MethodInvoker)delegate
            {
                // Running on the UI thread
                this.Close();
            });
        }


        private void btnEnviar_Click(object sender, EventArgs e)
        {
            //Recolha da msg do client para uma variavel
            string message = textBoxMessage.Text;

            message = textBoxUsername.Text + ": " + message;

            textBoxConversa.Invoke((MethodInvoker)delegate
            {

                // Running on the UI thread
                textBoxConversa.AppendText(Environment.NewLine);
                textBoxConversa.AppendText(message);
                textBoxConversa.AppendText(Environment.NewLine);
                textBoxMessage.Clear();
            });

            byte[] msgCifrada = CifrarTexto(message);

            byte[] assinatura = Assinar_Msg(msgCifrada);

            //Envio da mensagem do cliente para o servidor
            byte[] packet = protocolSI.Make(ProtocolSICmdType.DATA, msgCifrada);
            networkStream.Write(packet, 0, packet.Length);

            //enviar assinatura digital para o servidor
            byte[] packet1 = protocolSI.Make(ProtocolSICmdType.DATA, assinatura);
            networkStream.Write(packet1, 0, packet1.Length);

        }

        //criamos a hash e ciframos com a chave privada do cliente
        private byte[] Assinar_Msg(byte[] msg)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(msg);

                byte[] signature = rsa.SignHash(hash, CryptoConfig.MapNameToOID("SHA256"));

                return signature;
            }
        }


        // criação das chaves publicas e privadas
        private void Gerar_chaves()
        {
            rsa = new RSACryptoServiceProvider();
            publicKey = rsa.ToXmlString(false);
            privateKey = rsa.ToXmlString(true);
        }


        //funçao que envia a chave publica do cliente para o servidor
        private void Enviar_Chave_Publica()
        {
            try
            {
                //criação de um array de bytes com a mensagem a enviar e envio da mesma para o servidor
                byte[] packet = protocolSI.Make(ProtocolSICmdType.PUBLIC_KEY, publicKey);
                // Enviar mensagem
                networkStream.Write(packet, 0, packet.Length);

                while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
                {
                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Erro no envio de dados ao servidor.", "Error Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }





        // função recebe a chave simétrica do servidor
        private void Chave_simetrica(ProtocolSI protocol)
        {
            try
            {
                string keyEnc = protocolSI.GetStringFromData();

                byte[] dados = Convert.FromBase64String(keyEnc);
                //decifrar dados utilizando RSA (chave privada do cliente)
                byte[] dadosDec = rsa.Decrypt(dados, true);
                chaveSecreta = Encoding.UTF8.GetString(dadosDec);
                aes.Key = Convert.FromBase64String(chaveSecreta);

                ack = protocolSI.Make(ProtocolSICmdType.ACK);
                networkStream.Write(ack, 0, ack.Length);
            }
            catch (Exception)
            {
                MessageBox.Show("Erro na comunicação com o servidor.", "Error Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //função recebe o vetor de inicialiização do servidor
        private void Vetor_inicializacao(ProtocolSI protocol)
        {
            try
            {
                rsa.FromXmlString(privateKey);
                string IVEnc = protocolSI.GetStringFromData();
                byte[] dados = Convert.FromBase64String(IVEnc);
                //decifrar dados utilizando RSA (chave privada do servidor)
                byte[] dadosDec = rsa.Decrypt(dados, true);
                iv = Encoding.UTF8.GetString(dadosDec);
                aes.IV = Convert.FromBase64String(iv);

                ack = protocolSI.Make(ProtocolSICmdType.ACK);
                networkStream.Write(ack, 0, ack.Length);
            }
            catch (Exception)
            {
                MessageBox.Show("Erro na comunicação com o servidor.", "Error Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

        private byte[] CifrarTexto(string textoRaw)
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

            return textoCifrado;
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

        private void btnSair_Click(object sender, EventArgs e)
        {
            ack = protocolSI.Make(ProtocolSICmdType.EOT);
            networkStream.Write(ack, 0, ack.Length);
        }
    }
}





















