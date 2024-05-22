using EI.SI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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

            ///<summary> Enquanto nao receber a informacao para fechar a comunicacao</summary>
            while (protocolSI.GetCmdType() != ProtocolSICmdType.EOT)
            {

                int byteRead = networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                byte[] ack;

                ///<summary>Com este switch vamos determinar o tipo de msg que estamos a receber</summary>
                switch (protocolSI.GetCmdType())
                {
                    ///<summary>Recebi uma msg (string) </summary>
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

                        clientPublicKey = byteRead.ToString();
                        
                        
                        //criamos um ack
                        ack = protocolSI.Make(ProtocolSICmdType.ACK);

                        //devolvemos o ack para a stream
                        networkStream.Write(ack, 0, ack.Length);

                        break;
                }
            }

            networkStream.Close();

        }
    }
}
