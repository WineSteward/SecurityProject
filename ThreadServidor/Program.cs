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
using System.IO;


namespace ThreadServidor
{
    internal class Program
    {
        private const int PORT = 10000;
        private static TcpListener listener;
        private static List<TcpClient> tcpClientsList = new List<TcpClient>();
        private static string serverStarting = "------------Server Initiated-------------";
        private static string logFile = @"C:\Users\MMC\Documents\logFile.txt";

        static void Main(string[] args)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
            listener = new TcpListener(endPoint);


            //adicionar ao ficheiro txt o inicio de uma nova sessao
            if (!File.Exists(logFile))
                File.Create(logFile);

            listener.Start();
            Console.WriteLine(serverStarting);

            // Definir o stream de ligação ao ficheiro em modo append e para escrita
            using(FileStream fs = new FileStream(logFile, FileMode.Append, FileAccess.Write))
            {
                // Criar o buffer de texto de escrita
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    // Guardar os dados no ficheio com string
                    sw.WriteLine(serverStarting);
                }
            }
            
            

            int clientCounter = 0;

            while (true)
            {

                TcpClient clientAtual = listener.AcceptTcpClient();
                tcpClientsList.Add(clientAtual);
                clientCounter++;

                Console.WriteLine("Client {0} has connected to the server", clientCounter);
                
                // Definir o stream de ligação ao ficheiro em modo append e para escrita
                using (FileStream fs = new FileStream(logFile, FileMode.Append, FileAccess.Write))
                {
                    // Criar o buffer de texto de escrita
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        // Guardar os dados no ficheio com string
                        sw.WriteLine("Client {0} has connected to the server", clientCounter);
                    }
                }

                //Tratamento do cliente após estabelecer ligacao ao servidor
                ClientHandler clienteHandler = new ClientHandler(clientAtual, clientCounter, tcpClientsList);

                clienteHandler.Handle();
            }
        }
    }
}














