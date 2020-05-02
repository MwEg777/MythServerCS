using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Eto.Forms;
using Eto.Drawing;
using System.Reflection;
using System.Linq;

namespace MythServer
{
    class Server
    {

        Methods methods = new Methods();

        TcpListener server = null;

        public static MongoCRUD db;

        public Server(string ip, int port)
        {

            db = new MongoCRUD("AddressBook");

            IPAddress localAddr = IPAddress.Parse(ip);
            server = new TcpListener(localAddr, port);
            server.Start();
            StartListener();

        }
        public class MythForm : Form
        {
            public MythForm()
            {

                var layout = new PixelLayout();

                Label playersTitle = new Label { Text = "Players" };
                playersTitle.Font = new Font(FontFamilies.Sans, 15);
                layout.Add(playersTitle, 10, 10);

                ListBox playersList = new ListBox();
                playersList.Size = new Size(300, 300);
                layout.Add(playersList, 10, 50);

                Content = layout;

                Size = new Size(1000, 500);

            }

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

            Queue<Dictionary<string, string>> clientRequestsQueue = new Queue<Dictionary<string, string>>();

            Thread udpThread = new Thread(new ParameterizedThreadStart(PlayerUDPThread));
            udpThread.Start(player);

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

                                Dictionary<string, string> clientMessageDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(message);
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

                                            Dictionary<string, string> clientMessageDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(msg);
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

                        Queue<Dictionary<string, string>> requests = new Queue<Dictionary<string, string>>(clientRequestsQueue);

                        while (clientRequestsQueue.Count > 0)
                        {

                            foreach (Dictionary<string, string> request in requests)
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
        public void PlayerUDPThread(Object playerObject)
        {

            try
            {

                UdpClient udp = new UdpClient(4468);

                Byte[] bytes = new byte[1024];
                string data = "";

                Console.WriteLine("Started listening to UDP client requests ...");

                Player player = playerObject as Player;

                IPEndPoint playerIP = player.connection.Client.RemoteEndPoint as IPEndPoint;

                Queue<Dictionary<string, string>> clientRequestsQueue = new Queue<Dictionary<string, string>>();

                while (player.online)
                {

                    bytes = udp.Receive(ref playerIP);
                    string serverMessage = Encoding.ASCII.GetString(bytes);

                    try
                    {

                        string hex = BitConverter.ToString(bytes);
                        data = Encoding.ASCII.GetString(bytes);

                        Console.WriteLine("Parsing UDP client response..");
                        string[] messages = data.Split(new string[] { "$eof$" }, StringSplitOptions.None);

                        foreach (string message in messages)
                        {

                            try
                            {

                                if (string.IsNullOrEmpty(message)) //Skip empty messages
                                    continue;

                                Dictionary<string, string> clientMessageDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(message);
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

                        Queue<Dictionary<string, string>> requests = new Queue<Dictionary<string, string>>(clientRequestsQueue);

                        while (clientRequestsQueue.Count > 0)
                        {

                            foreach (Dictionary<string, string> request in requests)
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
                                    catch (Exception exo2)
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

                Console.WriteLine("UDP Thread stopped for disconnected player");

            }

            catch (SocketException socketException)
            {

                Console.WriteLine("Socket exception: " + socketException);

            }

        }

        public void ProcessClientMessage(Player player, Dictionary<string, string> clientMessage)
        {

            Console.WriteLine("Processing client message with type " + clientMessage["type"]);

            Type thisType = methods.GetType();
            MethodInfo theMethod = thisType.GetMethod(clientMessage["type"]);
            theMethod.Invoke(methods, new object[] { player, clientMessage });

        }

    }
}
