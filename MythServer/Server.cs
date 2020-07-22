using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Linq;

namespace MythServer
{
    class Server
    {

        Methods methods = new Methods();
        static UdpClient udp;
        TcpListener server = null;

        public static MongoCRUD db;

        public Server(string ip, int port)
        {

            db = new MongoCRUD("AddressBook");

            IPAddress localAddr = IPAddress.Parse(ip);
            server = new TcpListener(localAddr, port);
            server.Start();
            Thread udpThread = new Thread(UDPThread);
            udpThread.Start();
            StartListener();

        }

        public void StartListener()
        {

            try
            {

                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Connected!");
                    Thread t = new Thread(new ParameterizedThreadStart(PlayerThread));
                    t.Start(client);
                }

            }
            catch (SocketException e)
            {

                Console.WriteLine("SocketException: {0}", e);
                server.Stop();

            }

        }
        public void PlayerThread(Object obj)
        {

            TcpClient client = (TcpClient)obj;
            var stream = client.GetStream();
            string data;
            Byte[] bytes = new Byte[1024];
            int i;
            string buffer = "";
            Player player = methods.AddPlayer(client);

            Queue<Dictionary<string, object>> clientRequestsQueue = new Queue<Dictionary<string, object>>();

            try
            {
                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    string hex = BitConverter.ToString(bytes);
                    data = Encoding.ASCII.GetString(bytes, 0, i);
                    //Console.WriteLine("{1}: Received: {0}", data, Thread.CurrentThread.ManagedThreadId);
                    //string str = "Hey Device!";
                    //Byte[] reply = System.Text.Encoding.ASCII.GetBytes(str);
                    //stream.Write(reply, 0, reply.Length);

                    try
                    {

                        //Debug.Log("Parsing client response..");
                        string[] messages = data.Split(new string[] { "$eof$" }, StringSplitOptions.None);

                        foreach (string message in messages)
                        {

                            try
                            {

                                if (string.IsNullOrEmpty(message)) //Skip empty messages
                                    continue;

                                Dictionary<string, object> clientMessageDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
                                if (clientMessageDict.ContainsKey("type"))
                                    clientRequestsQueue.Enqueue(clientMessageDict);
                                else
                                    Console.WriteLine("Message doesn't contain type. It's most likely not from a game player!");

                            }
                            catch (Exception exx)
                            {

                                buffer += message.EndsWith("}") ? (message + "$eof$") : message;
                                Console.WriteLine("Message parsing problem. Trying to complete it from previous buffer. \n Exception: " + exx.Message);

                                if (!string.IsNullOrEmpty(buffer))
                                {

                                    string[] messagesBuffer = buffer.Split(new string[] { "$eof$" }, StringSplitOptions.None);

                                    foreach (string msg in messagesBuffer)
                                    {

                                        try
                                        {

                                            if (string.IsNullOrEmpty(msg)) //Skip empty messages
                                                continue;

                                            Dictionary<string, object> clientMessageDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(msg);
                                            if (clientMessageDict.ContainsKey("type"))
                                                clientRequestsQueue.Enqueue(clientMessageDict);
                                            else
                                                Console.WriteLine("Message doesn't contain type. It's most likely not from a game player!");

                                        }
                                        catch (Exception exx2)
                                        {

                                            Console.WriteLine("Couldn't parse one buffer message. \n Exception: " + exx2.Message);

                                        }

                                    }

                                }

                            }
                        }

                        Queue<Dictionary<string, object>> requests = new Queue<Dictionary<string, object>>(clientRequestsQueue);

                        while (clientRequestsQueue.Count > 0)
                        {

                            foreach (Dictionary<string, object> request in requests)
                                try
                                {
                                    ProcessClientMessage(player, clientRequestsQueue.Dequeue());
                                }
                                catch (Exception exo)
                                {

                                    try
                                    {

                                        Console.WriteLine("Problem processing a client message or dequeuing message. \n" +
                                            "request: " + request["type"].ToString() + " \n" +
                                            "Exception: " + exo.Message);

                                    }
                                    catch(Exception exo2)
                                    {

                                        Console.WriteLine("Problem processing a client message, Probably message is corrupted from client side.");

                                    }

                                }

                        }

                    }
                    catch (Exception ex)
                    {

                        Console.WriteLine("Couldn't parse client response. " + "\nException is: " + ex.ToString() + ", Client response is: " + data);

                    }

                }

                player.online = false;
                Console.WriteLine("Player disconnected!");

            }
            catch (Exception e)
            {

                Console.WriteLine("Exception: {0}", e.ToString());
                client.Close();

            }
        
        }
        public void UDPThread()
        {

            try
            {

                udp = new UdpClient(4467);

                Byte[] bytes = new byte[1024];
                string data = "";

                Console.WriteLine("Started listening to UDP requests ...");

                Queue<Dictionary<string, object>> clientRequestsQueue = new Queue<Dictionary<string, object>>();

                IPEndPoint udpRecvEndpoint = new IPEndPoint(IPAddress.Any, 4467);

                while (true)
                {

                    bytes = udp.Receive(ref udpRecvEndpoint);
                    string serverMessage = Encoding.ASCII.GetString(bytes);

                    try
                    {

                        string hex = BitConverter.ToString(bytes);
                        data = Encoding.ASCII.GetString(bytes);

                        //Console.WriteLine("Parsing UDP client response..");
                        string[] messages = data.Split(new string[] { "$eof$" }, StringSplitOptions.None);
                        //Console.WriteLine("Client UDP Connection info: IP is " + udpRecvEndpoint.Address + ", port is: " + udpRecvEndpoint.Port);

                        foreach (string message in messages)
                        {

                            //Console.WriteLine("Processing UDP message " + message);

                            try
                            {

                                if (string.IsNullOrEmpty(message)) //Skip empty messages
                                    continue;

                                Dictionary<string, object> clientMessageDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
                                if (clientMessageDict.ContainsKey("type"))
                                    clientRequestsQueue.Enqueue(clientMessageDict);
                                else
                                    Console.WriteLine("Message doesn't contain type. It's most likely not from a game player!");

                            }
                            catch (Exception exx)
                            {

                                Console.WriteLine("UDP message parsing problem. \n Exception: " + exx.Message);

                            }
                        }

                        Queue<Dictionary<string, object>> requests = new Queue<Dictionary<string, object>>(clientRequestsQueue);

                        while (clientRequestsQueue.Count > 0)
                        {

                            foreach (Dictionary<string, object> request in requests)
                            {
                                try
                                {
                                    
                                    Dictionary<string, object> dictToDequeue = clientRequestsQueue.Dequeue();

                                    Player player = methods.GetPlayerByID(request["playerid"].ToString());
                                    player.udpIPEndPoint.Port = udpRecvEndpoint.Port;
                                    ProcessClientMessage(player, dictToDequeue);
                                }
                                catch (Exception exo)
                                {

                                    try
                                    {

                                        Console.WriteLine("Problem processing a client message or dequeuing message. \n" +
                                        "request: " + request["type"].ToString() + " \n" +
                                        "Exception: " + exo.Message);


                                    }
                                    catch (Exception exo2)
                                    {

                                        Console.WriteLine("Problem processing a client message, Probably message is corrupted from client side.\n" + exo2);

                                    }

                                }

                            }

                        }

                    }
                    catch (Exception ex)
                    {

                        Console.WriteLine("Couldn't parse client response. " + "\nException is: " + ex.ToString() + ", Client response is: " + data);

                    }

                }

                Console.WriteLine("UDP Thread stopped for disconnected player");

            }

            catch (SocketException socketException)
            {

                Console.WriteLine("Socket exception: " + socketException);

            }

        }

        public void ProcessClientMessage(Player player, Dictionary<string, object> clientMessage)
        {

            //Console.WriteLine("Processing client message with type " + clientMessage["type"]);

            Type thisType = methods.GetType();
            MethodInfo theMethod = thisType.GetMethod(clientMessage["type"].ToString());
            theMethod.Invoke(methods, new object[] { player, clientMessage });

            player.secondsSinceLastValidMessage = Methods.secondsSinceStartUp;

        }

        public static void SendMessageUDP(Player player, string message)
        {

            byte[] messageAsByteArray = Encoding.ASCII.GetBytes(message);

            udp.Send(messageAsByteArray, messageAsByteArray.Length, player.udpIPEndPoint);

        }

        public static void SendMessageTCP(Player player, string message)
        {

            try
            {
                // Get a stream object for writing. 			
                NetworkStream stream = player.connection.GetStream();
                if (stream.CanWrite)
                {
                    // Convert string message to byte array.                 
                    byte[] clientMessageAsByteArray = Encoding.ASCII.GetBytes(message);
                    // Write byte array to socketConnection stream.                 
                    stream.Write(clientMessageAsByteArray, 0, clientMessageAsByteArray.Length);
                    //Debug.Log("Sending the following to server: " + msgToSend);
                }
            }
            catch (SocketException socketException)
            {
                Console.WriteLine("Socket exception: " + socketException);
            }
            catch(Exception ex)
            {
                Console.WriteLine("SendMessageTCP Exception: " + ex);
            }

        }

    }
}
