using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System.Net;
using System.Linq;
using Newtonsoft.Json;
using CoreExtensions;
using System.Threading.Tasks;
using System.Threading;

namespace MythServer
{
    class Methods
    {

        public List<Player> players = new List<Player>();
        public List<Room> rooms = new List<Room>();

        public static Methods instance;

        public static int secondsSinceStartUp = 0,
            playerTimeoutMax = 5;

        public Methods()
        {

            instance = this;
            Thread playersTimeoutCheckerThread = new Thread(PlayersTimeoutChecker);
            playersTimeoutCheckerThread.Start();

        }

        async void PlayersTimeoutChecker() 
        { 

	        while(true)
            {

                await Task.Delay(1000);

                secondsSinceStartUp++;

                try
                {

                    List<Player> playersToRemove = new List<Player>();

                    foreach(Player player in players)
                    {

                        //Console.WriteLine("Looping over player " + player.name + ". Time now is: " + secondsSinceStartUp + " and player time is: " + player.secondsSinceLastValidMessage);

                        if (secondsSinceStartUp - player.secondsSinceLastValidMessage > playerTimeoutMax || secondsSinceStartUp - player.secondsSinceLastValidMessage < - playerTimeoutMax)
                        {

                            try
                            {

                                try
                                {
                                    player.connection.Close();
                                    Console.WriteLine("Player " + player.name + " timed out. Disconnecting..");
                                    player.online = false;
                                    if (player.room != null)
                                    {
                                        Console.WriteLine("The disconnected player was in room " + player.room.id + ", removing him from it first.");
                                        player.room.RemovePlayerFromRoom(player);
                                    }
                                    playersToRemove.Add(player);
                                    Console.WriteLine("Player " + player.name + " disconnected.");
                                }
                                catch(Exception eClose)
                                {
                                    Console.WriteLine("Couldn't close connection with player " + player.name + ". " + eClose);
                                }

                            }
                            catch(Exception ex)
                            {
                                Console.WriteLine("Couldn't disconnect player and remove him. " + ex);
                            }

                        }

                    }

                    foreach (Player player in playersToRemove)
                        players.Remove(player);

                }
                catch(Exception e)
                {
                    Console.WriteLine("No IDEAR why an error happened here. " + e);
                }

            }

        }

        public Player AddPlayer(TcpClient conn)
        {

            Player player = new Player
            { connection = conn };
            player.udpIPEndPoint = (IPEndPoint)conn.Client.RemoteEndPoint;
            players.Add(player);
            return player;

        }

        public Player GetPlayerByID(string ID)
        {

            //Console.WriteLine("Players in list are: ");

            // foreach(Player player in players)
            //     Console.WriteLine("Player id: " + player.name);

            return players.Find(p => p.id == ID);

        }

        public Room CreateNewRoom()
        {

            Room room = new Room();
            room.id = Guid.NewGuid().ToString();
            room.roomState = Room.State.Matchmaking;
            rooms.Add(room);
            return room;

        }

        #region PlayerRequests

        public void WHOAMI(Player player, Dictionary<string, object> payload)
        {

            player.id = payload["id"].ToString();
            player.name = payload["name"].ToString();
            player.online = true;

        }

        public void UPDATE_INDICATOR(Player player, Dictionary<string, object> payload)
        {

            player.room.BroadcastInRoomToOpponentsOnly(JsonConvert.SerializeObject(payload), player, false);

        }

        public void FIRE_BULLET(Player player, Dictionary<string, object> payload)
        {

            Console.WriteLine("Player " + player.name + " Is firing a bullet. Grant state: " + player.room.FireBullet(payload["firevector"].ToString()));

        }

        public void START_MATCHMAKING(Player player, Dictionary<string, object> payload)
        {

            Room firstSuitableRoom = null;

            foreach (Room room in rooms)
                if (!room.isFull() && room.roomState == Room.State.Matchmaking)
                {
                    firstSuitableRoom = room;
                    break;
                }

            if (firstSuitableRoom == null)
                firstSuitableRoom = CreateNewRoom();

            firstSuitableRoom.AddPlayerToRoom(player);

        }

        public void CANCEL_MATCHMAKING(Player player, Dictionary<string, object> payload)
        {

            if (player.room == null)
                return;

            player.room.RemovePlayerFromRoom(player);

        }

        public void GET_DATA(Player player, Dictionary<string, object> payload)
        {

            Console.WriteLine("Payload type name: " + payload["type"]);
            player.id = payload["id"].ToString();

            var filter = Builders<PlayerDB>.Filter.Eq("id", player.id);

            if (!Server.db.PlayerExists(filter))
                Server.db.InsertPlayer("Users", new PlayerDB
                {

                    name = payload["name"].ToString(),
                    id = player.id

                });


            var update = Builders<PlayerDB>.Update.Set("name", payload["name"]);

            Server.db.UpdatePlayer(filter, update);

        }

        public void IAM_ALIVE(Player player, Dictionary<string, object> payload)
        {

            //Console.WriteLine("Player sent a pulse! Player ID is: " + player.name);

        }

        public void PLACE_GUARD(Player player, Dictionary<string, object> payload)
        {

            Console.WriteLine("Player " + player.name + " Is placing a guard. Grant state: " + player.room.PlaceGuard(payload["guardpos"].ToString()));

        }

        public void REQUEST_BULLET(Player player, Dictionary<string, object> payload)
        {

            Console.WriteLine("Player " + player.name + " requested a bullet. Grant state: " + player.room.GiveBullet());

        }

        public void REQUEST_ROLE(Player player, Dictionary<string, object> payload)
        {

            Console.WriteLine("Player " + player.name + " asked for his role.");
            foreach (KeyValuePair<Room.TurnOwner, Player> kvp in player.room.roles)
                if (kvp.Value.Equals(player))
                    Server.SendMessageTCP(kvp.Value, R_SETROLE(kvp.Key));

        }

        public void SEND_MY_INFO(Player player, Dictionary<string, object> payload)
        {

            Console.WriteLine("Player " + player.name + " just sent his info.");

            player.room.BroadcastInRoomToOpponentsOnly(R_SETOPPONENTINFO(payload) , player, true);

        }

        public void SEND_MY_STATE(Player player, Dictionary<string, object> payload)
        {

            Console.WriteLine("Player " + player.name + " just sent his state.");

            player.room.SetPlayerState(player, JsonConvert.DeserializeObject<Room.OpponentState>(payload["state"].ToString()));

        }

        public void UDP_PULSE(Player player, Dictionary<string, object> payload)
        {

            //Console.WriteLine("Player " + player.name + " updated his UDP port to: " + player.udpIPEndPoint.Port);

            //Server.SendMessageUDP(player, R_HEY());

        }

        #endregion


        #region ServerResponses

        public static string R_HEY()
        {

            Dictionary<string, object> toConvert = new Dictionary<string, object>();

            toConvert.Add("type", "Hey");
            
            return toConvert.ToJson();

        }

        public static string R_LOGMESSAGE(string message)
        {

            Dictionary<string, object> toConvert = new Dictionary<string, object>();

            toConvert.Add("type", "logmessage");
            toConvert.Add("message", message);

            return toConvert.ToJson();

        }

        public static string R_SETROLE(Room.TurnOwner role)
        {

            Dictionary<string, object> toConvert = new Dictionary<string, object>();

            toConvert.Add("type", "SET_ROLE");
            toConvert.Add("role", role);

            return toConvert.ToJson();

        }

        public static string R_SETOPPONENTINFO(Dictionary<string, object> payload)
        {

            Dictionary<string, object> toConvert = new Dictionary<string, object>();

            toConvert.Add("type", "SET_OPPONENT_INFO");
            toConvert.Add("name", payload["name"]);
            toConvert.Add("id", payload["id"]);
            //toConvert.Add("imagebase64", payload["imagebase64"]);

            return toConvert.ToJson();

        }

        public static string R_SETOPPONENTSTATE(Room.OpponentState state)
        {

            Dictionary<string, object> toConvert = new Dictionary<string, object>();

            toConvert.Add("type", "SET_OPPONENT_STATE");
            toConvert.Add("opponentstate", JsonConvert.SerializeObject(state));

            return toConvert.ToJson();

        }

        public static string R_STARTGAME(Room room)
        {

            Dictionary<string, object> toConvert = new Dictionary<string, object>();

            toConvert.Add("type", "START_GAME");

            return toConvert.ToJson();

        }

        public static string R_RESTARTGAME(Room room)
        {

            Dictionary<string, object> toConvert = new Dictionary<string, object>();

            toConvert.Add("type", "RESTART_GAME");

            return toConvert.ToJson();

        }

        public static string R_PLACEGUARD(string guardPos)
        {

            Dictionary<string, object> toConvert = new Dictionary<string, object>();

            toConvert.Add("type", "PLACE_GUARD");
            toConvert.Add("guardpos", guardPos);

            return toConvert.ToJson();

        }

        public static string R_FIREBULLET(string fireVector)
        {

            Dictionary<string, object> toConvert = new Dictionary<string, object>();

            toConvert.Add("type", "FIRE_BULLET");
            toConvert.Add("firevector", fireVector);

            return toConvert.ToJson();

        }

        public static string R_PLACEBULLET()
        {

            Dictionary<string, object> toConvert = new Dictionary<string, object>();

            toConvert.Add("type", "PLACE_BULLET");

            return toConvert.ToJson();

        }

        public static string R_SWITCHTURN(Room.TurnOwner turnOwner)
        {

            Dictionary<string, object> toConvert = new Dictionary<string, object>();

            toConvert.Add("type", "SWITCH_TURN");
            toConvert.Add("turn", turnOwner);

            return toConvert.ToJson();

        }

        #endregion

    }

    public class MongoCRUD
    {

        private IMongoDatabase db;

        public MongoCRUD(string database)
        {

            var client = new MongoClient();
            db = client.GetDatabase(database);

        }

        public void InsertPlayer<PlayerDB>(string table, PlayerDB playerToInsert)
        {

            var collection = db.GetCollection<PlayerDB>(table);
            collection.InsertOne(playerToInsert);

        }

        public void UpdatePlayer<PlayerDB>(FilterDefinition<PlayerDB> filter, UpdateDefinition<PlayerDB> update)
        {

            var collection = db.GetCollection<PlayerDB>("Users");
            collection.UpdateOne(filter, update);

        }

        public void RemovePlayer<PlayerDB>(FilterDefinition<PlayerDB> filter)
        {

            var collection = db.GetCollection<PlayerDB>("Users");
            collection.DeleteOne(filter);

        }

        public bool PlayerExists<PlayerDB>(FilterDefinition<PlayerDB> filter)
        {

            var collection = db.GetCollection<PlayerDB>("Users");
            return collection.Find<PlayerDB>(filter).Any();

        }
        
    }

    class PlayerDB
    {

        [BsonId]
        public Guid Id { get; set; }

        public string name, id;

        public TestClass test = new TestClass();

    }

    class TestClass
    {

        public string testData1 = "first";
        public string testData2 = "second";

    }

    class Player
    {

        #region Essentials

        public string id = "no_id", name = "no_name";
        public bool loaded = false, online = false;
        public TcpClient connection;
        public IPEndPoint udpIPEndPoint;
        public Room room;
        public long secondsSinceLastValidMessage = Methods.secondsSinceStartUp;

        #endregion

        #region GameSpecificStuff



        #endregion

    }

    class Room
    {

        #region Essentials

        public string id;
        public enum State { Matchmaking, Ongoing, Ended };
        public State roomState = State.Matchmaking;
        public List<Player> players = new List<Player>();
        public int maxPlayers = 2;

        #endregion

        #region GameSpecificStuff

        public int guardsLeft = 16, maxGuards = 16, bulletsLeft = 3, maxBullets = 3;
        public enum TurnOwner { Attacker, Defender };

        public TurnOwner turn = TurnOwner.Defender;

        public Dictionary<TurnOwner, Player> roles = new Dictionary<TurnOwner, Player>();
        public bool aBulletIsOut = false;

        public enum OpponentState { None, Again, Left };
        public Dictionary<Player, OpponentState> states = new Dictionary<Player, OpponentState>();

        #endregion

        public void AssignRoles()
        {

            roles.Clear();

            //Assign first player as defender, second as attacker.
            //Random. Based on who connects first.
            roles.Add(TurnOwner.Defender, players[0]);
            roles.Add(TurnOwner.Attacker, players[1]);

            states.Clear();
            
            foreach(Player player in players)
                states.Add(player, OpponentState.None);

        }

        public void ExchangeRoles()
        {

            if (roles[TurnOwner.Attacker] == players[0])
            {

                roles[TurnOwner.Defender] = players[0];
                roles[TurnOwner.Attacker] = players[1];

            }
            else
            {

                roles[TurnOwner.Attacker] = players[0];
                roles[TurnOwner.Defender] = players[1];

            }

            states.Clear();

            foreach(Player player in players)
                states.Add(player, OpponentState.None);


        }

        public void SetPlayerState(Player player, OpponentState state)
        {

            try
            {

                states[player] = state;
                BroadcastInRoomToOpponentsOnly(Methods.R_SETOPPONENTSTATE(state), player);

                //Check if all want to play again
                bool allAreReady = true;
                foreach(KeyValuePair<Player, OpponentState> kvp in states)
                    if (kvp.Value != OpponentState.Again)
                    {
                        allAreReady = false;
                        break;
                    }

                if (allAreReady)
                    RestartMatch();

            }
            catch(Exception ex)
            {

                Console.WriteLine("Couldn't set player state to " + state.ToString() + ". Exception: " + ex);

            }

        }

        public bool PlaceGuard(string guardPositionJson)
        {

            if (turn != TurnOwner.Defender)
                return false;

            if (guardsLeft > 0)
            {

                //Broadcast guard position to all players
                BroadcastInRoom(Methods.R_PLACEGUARD(guardPositionJson));

                guardsLeft--;

                if (guardsLeft <= 0)
                {

                    guardsLeft = 0;
                    GiveTurnTo(TurnOwner.Attacker);

                }

                return true;
            }
            else
                return false;

        }

        public bool FireBullet(string fireVector)
        {

            Console.WriteLine("Trying to fire a bullet in room. bullets left: " + bulletsLeft + " , turn: " + turn.ToString());

            if (turn != TurnOwner.Attacker)
                return false;

            if (!aBulletIsOut)
            {

                BroadcastInRoom(Methods.R_FIREBULLET(fireVector));

                aBulletIsOut = true;

                return true;
            }
            else
                return false;

        }

        public bool GiveBullet()
        {

            if (bulletsLeft > 0)
            {

                aBulletIsOut = false;

                BroadcastInRoom(Methods.R_PLACEBULLET());
                bulletsLeft--;

                return true;

            }

            return false;

        }

        public void GiveTurnTo(TurnOwner newTurnOwner)
        {

            turn = newTurnOwner;

            if (turn == TurnOwner.Defender)
                guardsLeft = maxGuards;
            else
            {
                bulletsLeft = maxBullets;
            }

            BroadcastInRoom(Methods.R_SWITCHTURN(newTurnOwner));

        }

        public bool isFull()
        {

            return players.Count == maxPlayers;

        }

        public bool AddPlayerToRoom(Player player)
        {
            
            if (isFull())
            {

                Console.WriteLine("Player " + player.name + " tried to join room " + id + " but it's full.");
                return false;

            }
            else
            {

                foreach(Room room in Methods.instance.rooms) //Remove player from any other room he's in
                {

                    foreach(Player p in room.players)
                    {
                        if (p.id == player.id)
                        {

                            Console.WriteLine("Player " + p.name + " was found in another room! Removing him from it..");
                            RemovePlayerFromRoom(player);

                        }
                    }

                }

                players.Add(player);
                Console.WriteLine("Player " + player.name + " joined room " + id);
                Server.SendMessageTCP(player, Methods.R_LOGMESSAGE("You just joined room " + id));
                player.room = this;
                player.loaded = false;

                //Check if room is ready to start the match
                if (isFull())
                {

                    Console.WriteLine("Room " + id + "\nis now ready to start the match!");
                    StartMatch();

                }

                PrintRoomInfo();

                return true;

            }


        }

        public void StartMatch()
        {

            Console.WriteLine("Room " + id + "\n match started!");
            roomState = State.Ongoing;
            AssignRoles();
            BroadcastInRoom(Methods.R_STARTGAME(this));
            GiveTurnTo(TurnOwner.Defender);

        }

        public void RestartMatch()
        {

            Console.WriteLine("Room " + id + "\n match restarted!");
            roomState = State.Ongoing;
            ExchangeRoles();
            BroadcastInRoom(Methods.R_RESTARTGAME(this));
            GiveTurnTo(TurnOwner.Defender);

        }

        public void EndMatch()
        {

            Console.WriteLine("Room " + id + "\n match ended!");
            roomState = State.Ended;
            //TODO: Broadcast that match ended and redirect to menu screen

        }

        public void RemovePlayerFromRoom(Player player)
        {

            try
            {
                SetPlayerState(player, Room.OpponentState.Left);
            }
            catch(Exception ex)
            {

                Console.WriteLine("Couldn't set player state while removing player from room. " + ex);

            }

            Console.WriteLine("Trying to remove player " + player.name + " from room " + id);
            if (players.Remove(players.Find(p => p.id == player.id)))
                Console.WriteLine("Player " + player.name + " left room " + id);
            else
                Console.WriteLine("Player " + player.name + " couldn't leave room " + id);

            //Check if room is now empty, then remove it from rooms list.
            if (players.Count == 0)
            {

                Console.WriteLine("Removing room because it's now empty.");
                Methods.instance.rooms.Remove(this);

            }

            PrintRoomInfo();

        }

        public void PrintRoomInfo()
        {

            Console.WriteLine("Room ID: " + id);
            Console.WriteLine("Room players (" + players.Count + "):");
            foreach(Player player in players)
                Console.WriteLine("     Player " + player.name);

        }

        public void BroadcastInRoom(string messageToBroadcast, bool tcp = true)
        {

            foreach(Player player in players)
            {

                try
                {

                    if (tcp)
                        Server.SendMessageTCP(player, messageToBroadcast);
                    else
                        Server.SendMessageUDP(player, messageToBroadcast);

                }
                catch(Exception ex)
                {

                    Console.WriteLine("Couldn't broadcast room message using " + (tcp? "tcp" : "udp") + " to one player: " + player.name + ".\nException: " + ex);

                }

            }

        }

        public void BroadcastInRoomToOpponentsOnly(string messageToBroadcast, Player sender, bool tcp = true)
        {

            foreach (Player player in players)
            {

                try
                {

                    if (player == sender)
                        continue;

                    if (tcp)
                        Server.SendMessageTCP(player, messageToBroadcast);
                    else
                        Server.SendMessageUDP(player, messageToBroadcast);

                }
                catch (Exception ex)
                {

                    Console.WriteLine("Couldn't broadcast room message to one player: " + player.name + ".\nException: " + ex);

                }

            }

        }


    }

}
