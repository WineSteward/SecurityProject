using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using EI.SI;


namespace ThreadServidor
{
    internal class Program
    {
        private const int PORT = 10000;

        static void Main(string[] args)
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, PORT);
            TcpListener listener = new TcpListener(endpoint);

            listener.Start();
            Console.WriteLine("Server Initiated");

            int clientCounter = 0;

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                clientCounter++;

                Console.WriteLine("Client {0} has connected to the server", clientCounter);

                //Tratamento do cliente após estabelecer ligacao ao servidor
                ClientHandler clienteHandler = new ClientHandler(client, clientCounter);

                clienteHandler.Handle();
            }
        }
    }
}














