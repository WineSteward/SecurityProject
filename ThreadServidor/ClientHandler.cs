using EI.SI;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace ThreadServidor
{
    internal class ClientHandler
    {
        List<TcpClient> clients;
        private TcpClient clientAtual;
        private int clientID;
        private string key;
        private string iv;
        private string Chave_publica1;
        private string Chave_publica2;
        private string Chave_simetrica1;
        private string Chave_simetrica2;
        private string vetorinicializacao_1;
        private string vetorinicializacao_2;
        private string logFile = @"C:\Users\MMC\Documents\logFile.txt";


        public ClientHandler(TcpClient clientAtual, int clientID, List<TcpClient> clients)
        {
            this.clientAtual = clientAtual;
            this.clients = clients;
            this.clientID = clientID;
        }


        public void Handle()
        {
            Thread thread = new Thread(threadHandler);
            thread.Start();
        }

        private void threadHandler()
        {
            NetworkStream networkStream = clientAtual.GetStream();
            ProtocolSI protocolSI = new ProtocolSI();

            //Enquanto nao receber a informacao para fechar a comunicacao
            while (protocolSI.GetCmdType() != ProtocolSICmdType.EOT)
            {

                int byteRead = networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                byte[] ack;

                //Com este switch vamos determinar o tipo de msg que estamos a receber
                switch (protocolSI.GetCmdType())
                {

                    // caso o protocolo que o cliente enviar for PUBLIC_KEY ele retorna a chave simetrica e o vetor de inicialização 
                    case ProtocolSICmdType.PUBLIC_KEY:
                        if (clientID == 1)
                        {
                            Chave_publica1 = protocolSI.GetStringFromData().Replace("\0", string.Empty);

                            //cria a chave simetrica
                            Chave_simetrica1 = Gerarchavesimetrica(Chave_publica1);
                            
                            //cria o vetor de inicialização
                            vetorinicializacao_1 = Gerarvetorinicializacao(Chave_publica1);

                            //devolucao do ack por parte do servidor para o cliente
                            ack = protocolSI.Make(ProtocolSICmdType.ACK);
                            networkStream.Write(ack, 0, ack.Length);


                            // envia a chave simetrica cifrada com a chave publica para o cliente
                            ack = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, Cifrar_com_chave_publica(Chave_simetrica1, Chave_publica1));
                            networkStream.Write(ack, 0, ack.Length);

                            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
                            {
                                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                            }

                            //envia o vetor inicializacao cifrado com a chave publica para o cliente
                            ack = protocolSI.Make(ProtocolSICmdType.USER_OPTION_2, Cifrar_com_chave_publica(vetorinicializacao_1, Chave_publica1));
                            networkStream.Write(ack, 0, ack.Length);

                            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
                            {
                                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                            }
                        }
                        else if (clientID == 2)
                        {
                            Chave_publica2 = protocolSI.GetStringFromData();

                            //cria o vetor de inicialização
                            vetorinicializacao_2 = Gerarvetorinicializacao(Chave_publica2);
                            
                            //cria a chave simetrica
                            Chave_simetrica2 = Gerarchavesimetrica(Chave_publica2);

                            //devolucao do ack por parte do servidor para o cliente
                            ack = protocolSI.Make(ProtocolSICmdType.ACK);
                            networkStream.Write(ack, 0, ack.Length);

                            // envia a chave simetrica e o vetor para o cliente
                            ack = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, Cifrar_com_chave_publica(Chave_simetrica2, Chave_publica2));
                            networkStream.Write(ack, 0, ack.Length);

                            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
                            {
                                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                            }

                            ack = protocolSI.Make(ProtocolSICmdType.USER_OPTION_2, Cifrar_com_chave_publica(vetorinicializacao_2, Chave_publica2));
                            networkStream.Write(ack, 0, ack.Length);

                            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
                            {
                                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                            }
                        }
                    break;

                    //Recebi uma msg (string)
                    case ProtocolSICmdType.DATA:

                        if (clientID == 1)
                        {
                            //recebe a mensagem do cliente
                            byte[] mensagemCifrada = protocolSI.GetData();

                            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

                            //receber a assinatura do cliente
                            byte[] assinatura = protocolSI.GetData();
                            
                            //fazer o hash da msg para comparar com a assinatura
                            byte[] hash = Hash_Dados(mensagemCifrada);

                            //comparar os dois
                            if (Verificar_Assinatura(assinatura, hash, Chave_publica1))
                            {
                                key = Chave_simetrica1;
                                iv = vetorinicializacao_1;

                                //decifra a mensagem do cliente
                                string mensagemDecifrada = DecifrarTexto(mensagemCifrada, key, iv);

                                // Definir o stream de ligação ao ficheiro em modo append e para escrita
                                using (FileStream fs = new FileStream(logFile, FileMode.Append, FileAccess.Write))
                                {
                                    // Criar o buffer de texto de escrita
                                    using (StreamWriter sw = new StreamWriter(fs))
                                    {

                                        // Guardar os dados no ficheio com string
                                        sw.WriteLine(mensagemDecifrada);
                                    }
                                }

                                // enviar msg para a consola do servidor com o ID do client e a msg
                                Console.WriteLine(mensagemDecifrada);

                                BroadCast(mensagemDecifrada, clientAtual, Chave_simetrica2, vetorinicializacao_2);
                            }
                            else
                            {
                                // enviar msg para a consola do servidor a dizer que houve um erro
                                Console.WriteLine("Erro na confirmação da autenticidade da mensagem do cliente emissor");
                            }

                        }
                        else
                        {
                            //recebe a mensagem do cliente
                            byte[] mensagemCifrada = protocolSI.GetData();

                            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

                            //receber a assinatura do cliente
                            byte[] assinatura = protocolSI.GetData();

                            //fazer o hash da msg para comparar com a assinatura
                            byte[] hash = Hash_Dados(mensagemCifrada);

                            //comparar os dois
                            if (Verificar_Assinatura(assinatura, hash, Chave_publica2))
                            {
                                key = Chave_simetrica2;
                                iv = vetorinicializacao_2;

                                //decifra a mensagem do cliente
                                string mensagemDecifrada = DecifrarTexto(mensagemCifrada, key, iv);

                                // Definir o stream de ligação ao ficheiro em modo append e para escrita
                                using (FileStream fs = new FileStream(logFile, FileMode.Append, FileAccess.Write))
                                {
                                    // Criar o buffer de texto de escrita
                                    using (StreamWriter sw = new StreamWriter(fs))
                                    {
                                        // Guardar os dados no ficheio com string
                                        sw.WriteLine(mensagemDecifrada);
                                     }
                                }

                                // enviar msg para a consola do servidor com o ID do client e a msg
                                Console.WriteLine("\n" + mensagemDecifrada);

                                BroadCast(mensagemDecifrada, clientAtual, Chave_simetrica1, vetorinicializacao_1);
                            }
                            else
                            {
                                // enviar msg para a consola do servidor a dizer que houve um erro
                                Console.WriteLine("Erro na confirmação da autenticidade da mensagem do cliente emissor");
                            }
                        }
                        break;
                }
            }

            byte[] eot = protocolSI.Make(ProtocolSICmdType.EOT);
            networkStream.Write(eot, 0, eot.Length);

            string mensagem = "Cliente " + clientID + " terminou a sua sessão";

            // Definir o stream de ligação ao ficheiro em modo append e para escrita
            using (FileStream fs = new FileStream(logFile, FileMode.Append, FileAccess.Write))
            {
                // Criar o buffer de texto de escrita
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    // Guardar os dados no ficheio com string
                    sw.WriteLine(mensagem);
                }
            }

            // enviar msg para a consola do servidor com o ID do client e a msg
            Console.WriteLine(mensagem);
        }

        //envia a mensagem para o cliente correto nao repetindo a mensagem do lado do cliente emissor
        public void BroadCast(string msg, TcpClient excludeClient, string chave, string iv)
        {
            ProtocolSI protocolSI = new ProtocolSI();
            byte[] ack;
            NetworkStream networkStream;

            foreach (TcpClient client in clients)
            {
                if (client != excludeClient)
                {
                    networkStream = client.GetStream();

                    //string mensagemCifrada = CifrarTexto(msg, chave, iv);

                    // envia a msg do cliente emissor para o cliente recetor de forma segura
                    ack = protocolSI.Make(ProtocolSICmdType.USER_OPTION_9, msg);
                    networkStream.Write(ack, 0, ack.Length);

                }
            }
        }

        private bool Verificar_Assinatura(byte[] signature, byte[] hash, string publicKey)
        {
            RSACryptoServiceProvider rsaVerify = new RSACryptoServiceProvider();

            rsaVerify.FromXmlString(publicKey); //dar a mesma chave publica do emissor ao recetor

            bool verify = rsaVerify.VerifyHash(hash, CryptoConfig.MapNameToOID("SHA256"), signature);

            return verify;
        }

        //criamos a hash a partir dos dados
        private byte[] Hash_Dados(byte[] msgCifrada)
        {
            using (SHA256 sha256 = SHA256.Create())
            {                
                byte[] hash = sha256.ComputeHash(msgCifrada);

                return hash;
            }
        }

        // função de cifra todo o texto que é para enviar para os clientes
        private static string CifrarTexto(string txt, string key, string iv)
        {
            AesCryptoServiceProvider aes = new AesCryptoServiceProvider();

            aes.Key = Convert.FromBase64String(key);
            aes.IV = Convert.FromBase64String(iv);
            //variavel para guardar o texto decifrado em bytes
            byte[] txtDecifrado = Encoding.UTF8.GetBytes(txt);

            //variavel para guardar o texto cifrado em bytes
            byte[] txtcifrado;

            //reservar espaço em memória para por la o texto a cifrá-lo
            MemoryStream ms = new MemoryStream();

            //inicializar o sistema de Cifragem(write)
            CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);

            //cifrar os dados
            cs.Write(txtDecifrado, 0, txtDecifrado.Length);
            cs.Close();

            //guardar os dados cifrados que estão em memória
            txtcifrado = ms.ToArray();

            //converter os bytes para Base64 (texto)
            string txtCifradob64 = Convert.ToBase64String(txtcifrado);

            //devolver os bytes cifrados em Base64(texto)
            return txtCifradob64;
        }


        //função que decifra todo o texto que está cifrado que é enviado do cliente. 
        private static string DecifrarTexto(byte[] txtcifrado, string key, string iv)
        {
            AesCryptoServiceProvider aes = new AesCryptoServiceProvider();

            aes.Key = Convert.FromBase64String(key);
            aes.IV = Convert.FromBase64String(iv);

            //reservar espaço em memória para por la o texto a decifrá-lo
            MemoryStream ms = new MemoryStream(txtcifrado);

            //inicializar o sistema de Decifragem(Read)
            CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);

            //variavel para guardar o texto decifrado em bytes
            byte[] plainbytes = new byte[ms.Length];

            //decifrar os dados
            cs.Read(plainbytes, 0, plainbytes.Length);
            cs.Close();

            //converter os bytes para UTF8 (texto)
            string txtdecifradob64 = Encoding.UTF8.GetString(plainbytes);

            //devolver os bytes decifrados em UTF8(texto)
            return txtdecifradob64;

        }

        //cifra os dados com a chave publica e retorna-os
        public static string Cifrar_com_chave_publica(string key, string publickey)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(publickey);
            //obter chave simétrica para cifrar
            byte[] dados = Encoding.UTF8.GetBytes(key);
            //cifrar daos utilizando RSA
            byte[] dadosEnc = rsa.Encrypt(dados, true);
            return Convert.ToBase64String(dadosEnc);
        }

        //fução que gere a chave simétrica 
        private static string Gerarchavesimetrica(string pass)
        {
            byte[] salt = new byte[] { 0, 1, 0, 8, 1, 9, 9, 7 };
            Rfc2898DeriveBytes pwdGen = new Rfc2898DeriveBytes(pass, salt, 1000);

            // Gerar a key (chave)
            byte[] key = pwdGen.GetBytes(16);

            //converter a password em base64
            string passB64 = Convert.ToBase64String(key);

            //devolver
            return passB64;
        }

        //função que gere o vetor de inicialização
        private static string Gerarvetorinicializacao(string pass)
        {
            byte[] salt = new byte[] { 6, 3, 7, 8, 0, 1, 2, 3 };
            Rfc2898DeriveBytes pwdGen = new Rfc2898DeriveBytes(pass, salt, 1000);

            // Gerar a vetor
            byte[] iv = pwdGen.GetBytes(16);

            //converter a password em base64
            string ivB64 = Convert.ToBase64String(iv);

            //devolver
            return ivB64;
        }




    }
}
