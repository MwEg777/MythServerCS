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

namespace MythServer
{
    class Methods
    {

        public List<Player> players = new List<Player>();

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

            return players.Find(p => p.id == ID);

        }

        #region PlayerRequests

        public void GET_DATA(Player player, Dictionary<string, string> payload)
        {

            Console.WriteLine("Payload type name: " + payload["type"]);
            player.id = payload["id"];

            var filter = Builders<PlayerDB>.Filter.Eq("id", player.id);

            if (!Server.db.PlayerExists(filter))
                Server.db.InsertPlayer("Users", new PlayerDB
                {

                    name = payload["name"],
                    id = player.id

                });


            var update = Builders<PlayerDB>.Update.Set("name", payload["name"]);

            Server.db.UpdatePlayer(filter, update);

        }

        public void IAM_ALIVE(Player player, Dictionary<string, string> payload)
        {

            Console.WriteLine("Player sent a pulse! Player ID is: " + player.id);

        }

        public void UDPPULSE(Player player, Dictionary<string, string> payload)
        {

            Console.WriteLine("Player " + player.id + " updated his UDP port to: " + player.udpIPEndPoint.Port);

            Server.SendMessageUDP(player, R_HEY());

        }

        #endregion


        #region ServerResponses

        public static string R_HEY()
        {

            Dictionary<string, string> toConvert = new Dictionary<string, string>();

            toConvert.Add("type", "heylol");
            
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

        public string id;
        public bool loaded = false, online = true;
        public TcpClient connection;
        public IPEndPoint udpIPEndPoint;

    }

}
