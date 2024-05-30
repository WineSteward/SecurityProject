using EI.SI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadServidor
{
    internal class ClientHandler
    {

        private TcpClient clientAtual;
        private int clientID;
        private string clientPublicKey;
        private byte[] key;
        private byte[] iv;
        AesCryptoServiceProvider aes;
        private RSACryptoServiceProvider rsa;

        public ClientHandler(TcpClient clientAtual, int clientID)
        {
            this.clientAtual = clientAtual;
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
                    //Recebi uma msg (string)
                    case ProtocolSICmdType.DATA:

                        //decifragem da mensagem recebida do cliente
                        string mensagemDecifrada = DecifrarTexto(protocolSI.GetStringFromData());

                        // enviar msg para a consola do servidor com o ID do client e a msg
                        Console.WriteLine("Cliente" + clientID + " : " + mensagemDecifrada);

                        //criamos um ack
                        ack = protocolSI.Make(ProtocolSICmdType.ACK);

                        //devolvemos o ack para a stream
                        networkStream.Write(ack, 0, ack.Length);

                        break;

                    case ProtocolSICmdType.EOT:

                        Console.WriteLine("Ending thread from Client" + clientID);

                        //criamos um ack
                        ack = protocolSI.Make(ProtocolSICmdType.ACK);

                        //devolvemos o ack para a stream
                        networkStream.Write(ack, 0, ack.Length);

                        break;

                    case ProtocolSICmdType.PUBLIC_KEY:

                        //guardar a public key
                        clientPublicKey = protocolSI.GetStringFromData();

                        //INICIALIZAR O SERVIÇO DE CIFRAGEM AES
                        aes = new AesCryptoServiceProvider();

                        //instanciar o rsa
                        rsa = new RSACryptoServiceProvider();

                        //Gerar chave sym e iv a partir da chave publica do cliente
                        key = GerarChaveSym(clientPublicKey);

                        iv = GerarVetorInicializacao(clientPublicKey);

                        //GUARDAR Sym Key e IV Gerados
                        aes.Key = key;
                        aes.IV = iv;

                        //CIFRAR IV E SYMKEY UTILIZANDO O ALGORITMO RSA
                        byte[] cypherdSymKey = rsa.Encrypt(key, true);
                        byte[] cypherdIV = rsa.Encrypt(iv, true);

                        string cypherdSymKeyB64 = Convert.ToBase64String(cypherdSymKey);
                        string cypherdIVB64 = Convert.ToBase64String(cypherdIV);


                        //preparacao do envio da chave simetrica
                        byte[] packetPK = protocolSI.Make(ProtocolSICmdType.PUBLIC_KEY, cypherdSymKeyB64);
                        networkStream.Write(packetPK, 0, packetPK.Length);


                        //preparacao do envio do IV
                        byte[] packetIV = protocolSI.Make(ProtocolSICmdType.IV, cypherdIVB64);
                        networkStream.Write(packetIV, 0, packetIV.Length);


                        break;
                }
            }

            networkStream.Close();

        }

        //FUNCAO PARA GERAR UMA CHAVE SIMETRICA A PARTIR DE UMA STRING (segredo)
        private byte[] GerarChaveSym(string segredo)
        {
            byte[] salt = new byte[] { 0, 1, 0, 8, 2, 9, 0, 5 };

            Rfc2898DeriveBytes passwordGeneration = new Rfc2898DeriveBytes(segredo, salt, 1000);

            //Gerar chave
            byte[] key = passwordGeneration.GetBytes(16);

            return key;
        }

        //FUNCAO PARA GERAR VETOR DE INICIALIZACAO A PARTIR DE UMA STRING (segredo)
        private byte[] GerarVetorInicializacao(string segredo)
        {
            //mudamos os salt para aumentar ainda mais a segurança
            byte[] salt = new byte[] { 8, 6, 5, 2, 1, 9, 0, 8 };

            Rfc2898DeriveBytes passwordGeneration = new Rfc2898DeriveBytes(segredo, salt, 1000);

            //Gerar chave
            byte[] iv = passwordGeneration.GetBytes(16);

            return iv;
        }


        //FUNCAO PARA CIFRAR QUALQUER STRING COM A CHAVE SIMETRICA
        private string CifrarMensagem(string textoRaw)
        {
            //conversao do textoRaw em bytes de BASE64
            byte[] textoRaw64 = Encoding.UTF8.GetBytes(textoRaw);

            //Reservar espaço na memoria para guardar o texto e cifra lo
            MemoryStream ms = new MemoryStream();

            //Inicializar o sitema de cifragem (write)
            CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);

            //Cifrar os dados
            cs.Write(textoRaw64, 0, textoRaw64.Length);
            cs.Close();

            //variavel para guardar o texto cifrado em bytes
            //Guardar os dados cifrados que estao na memoria
            byte[] textoCifrado = ms.ToArray();

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
