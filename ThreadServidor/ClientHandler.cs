﻿using EI.SI;
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

                        // enviar msg para a consola do servidor com o ID do client e a msg
                        Console.WriteLine("Cliente" + clientID + " : " + protocolSI.GetStringFromData());

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

                        //é necessario mandar ack? Quando é que mandamos ack

                        //criamos um ack
                        ack = protocolSI.Make(ProtocolSICmdType.ACK);

                        //devolvemos o ack para a stream
                        networkStream.Write(ack, 0, ack.Length);

                        //INICIALIZAR O SERVIÇO DE CIFRAGEM AES
                        aes = new AesCryptoServiceProvider();

                        //GUARDAR Sym Key e IV Gerados
                        key = aes.Key;

                        iv = aes.IV;

                        //Gerar chave sym e iv a partir da chave publica do cliente
                        key = GerarChaveSym(clientPublicKey);

                        iv = GerarVetorInicializacao(clientPublicKey);

                        //cifragem da chave simetrica
                        string cypherdSymKey = CifrarMensagem(key.ToString());

                        //preparacao do envio da chave simetrica
                        byte[] packet = protocolSI.Make(ProtocolSICmdType.PUBLIC_KEY, cypherdSymKey);
                        
                        //envio da chave sym cifrada para a stream
                        networkStream.Write(packet, 0, packet.Length);

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
    }
}
