using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MasterServer
{
    class MasterServerMessageHandler : Ripped.RippedMessageHandler
    {
        private class ServerStatus
        {
            public string Username { get; set; }
            public string UserId { get; set; }

            public string ServerName { get; set; }
            public string Secret { get; set; }
            public byte[] PublicKey { get; set; }
            public byte[] Random { get; set; }
            public int CurrentPlayerCount { get; set; }
            public int MaxPlayerCount { get; set; }
            public DiscoveryPolicy DiscoveryPolicy { get; set; }
            public InvitePolicy InvitePolicy { get; set; }
            public GameplayServerConfiguration Configuration { get; set; }
            public string Password { get; set; }
            public IPEndPoint EndPoint { get; set; }
        }

        private Task<IDiffieHellmanKeyPair> _keysTask = DiffieHellmanUtility.GenerateKeysAsync(DiffieHellmanUtility.KeyType.ElipticalCurve);
        private PacketEncryptionLayer _encryptionLayer;

        private Dictionary<IPEndPoint, byte[]> serverRandoms = new Dictionary<IPEndPoint, byte[]>();
        private Dictionary<IPEndPoint, byte[]> clientRandoms = new Dictionary<IPEndPoint, byte[]>();
        private Dictionary<IPEndPoint, IDiffieHellmanKeyPair> serverKeys = new Dictionary<IPEndPoint, IDiffieHellmanKeyPair>();

        private Dictionary<string, ServerStatus> serverList = new Dictionary<string, ServerStatus>();

        public MasterServerMessageHandler(IMessageSender sender, PacketEncryptionLayer encryptionLayer) : base(sender, encryptionLayer)
        {
            RegisterUserMessageHandlers();
            _encryptionLayer = encryptionLayer;
        }

        #region SHOULDHANDLE
        protected override bool ShouldHandleUserMessage(IUserMessage packet, MessageOrigin origin)
        {
            return true;
        }

        protected override bool ShouldHandleHandshakeMessage(IHandshakeMessage packet, MessageOrigin origin)
        {
            return packet is IHandshakeClientToServerMessage;
        }
        #endregion SHOULDHANDLE

        #region AUTHENTICATION
        protected override void HandleClientHelloRequest(ClientHelloRequest packet, MessageOrigin origin)
        {
            if (!clientRandoms.ContainsKey(origin.endPoint)) clientRandoms[origin.endPoint] = packet.random;

            var random = PacketEncryptionLayer.GenerateRandom(32);
            SendUnreliableResponse(1u, origin.endPoint, packet, HelloVerifyRequest.pool.Obtain().Init(random));
            packet.Release();
        }

        protected override async void HandleClientHelloWithCookieRequest(ClientHelloWithCookieRequest packet, MessageOrigin origin)
        {
            var fakeSignature = PacketEncryptionLayer.GenerateRandom(128);

            if (!serverRandoms.ContainsKey(origin.endPoint)) serverRandoms[origin.endPoint] = PacketEncryptionLayer.GenerateRandom(32);
            if (!serverKeys.ContainsKey(origin.endPoint)) serverKeys[origin.endPoint] = await _keysTask;
            
            SendReliableResponse(1u, origin.endPoint, packet, ServerHelloRequest.pool.Obtain().Init(serverRandoms[origin.endPoint], serverKeys[origin.endPoint].publicKey, fakeSignature));
            SendReliableResponse(1u, origin.endPoint, packet.certificateResponseId, ServerCertificateRequest.pool.Obtain().Init(new List<byte[]>
            {
                serverRandoms[origin.endPoint]
            }));
            packet.Release();
        }

        protected override async void HandleClientKeyExchangeRequest(ClientKeyExchangeRequest packet, MessageOrigin origin)
        {
            var preMasterSecret = await serverKeys[origin.endPoint].GetPreMasterSecretAsync(packet.clientPublicKey);

            SendReliableResponse(1u, origin.endPoint, packet, ChangeCipherSpecRequest.pool.Obtain());

            _encryptionLayer.AddEncryptedEndpoint(1u, origin.endPoint, null, null, preMasterSecret, serverRandoms[origin.endPoint], clientRandoms[origin.endPoint], false);

            packet.Release();
        }

        protected override void HandleAuthenticateUserRequest(AuthenticateUserRequest packet, MessageOrigin origin)
        {
            SendReliableResponse(1u, origin.endPoint, packet, AuthenticateUserResponse.pool.Obtain().Init(AuthenticateUserResponse.Result.Success));
            packet.Release();
        }
        #endregion AUTHENTICATION

        #region HEARTBEAT
        protected override void HandleBroadcastServerHeartbeatRequest(BroadcastServerHeartbeatRequest packet, MessageOrigin origin)
        {
            SendUnreliableMessage(1u, origin.endPoint, BroadcastServerHeartbeatResponse.pool.Obtain().Init(BroadcastServerHeartbeatResponse.Result.Success));

            packet.Release();
        }
        #endregion HEARTBEAT

        #region SERVER CREATION / DELETION
        protected override void HandleBroadcastServerStatusRequest(BroadcastServerStatusRequest packet, MessageOrigin origin)
        {
            //I'm assuming this only happens on server creation, and that this packet doesn't
            //also get sent on updating any sort of server status. Under that assumption, this should be fine.
            var code = RandomString(5);

            serverList[code] = new ServerStatus
            {
                Username = packet.userName,
                UserId = packet.userId,

                ServerName = packet.serverName,
                Secret = packet.secret,
                PublicKey = packet.publicKey,
                Random = packet.random,
                CurrentPlayerCount = packet.currentPlayerCount,
                MaxPlayerCount = packet.maxPlayerCount,
                DiscoveryPolicy = packet.discoveryPolicy,
                InvitePolicy = packet.invitePolicy,
                Configuration = packet.configuration,
                Password = packet.password,
                EndPoint = origin.endPoint
            };

            SendReliableResponse(1u, origin.endPoint, packet, BroadcastServerStatusResponse.pool.Obtain().InitForSuccess(origin.endPoint, code));

            packet.Release();
        }

        protected override void HandleBroadcastServerRemoveRequest(BroadcastServerRemoveRequest packet, MessageOrigin origin)
        {
            //TODO: Time complexity please.
            serverList.Remove(serverList.First(x => x.Value.Secret == packet.secret).Key);

            packet.Release();
        }

        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        #endregion SERVER CREATION / DELETION

        #region SERVER JOIN BY CODE
        protected override void HandleConnectToServerRequest(ConnectToServerRequest packet, MessageOrigin origin)
        {
            if (serverList.TryGetValue(packet.code, out var server))
            {
                var isConnectionOwner = packet.userId == server.UserId;
                var correctPassword = server.Password != string.Empty ? packet.password == server.Password : true;
                if (correctPassword)
                {
                    SendReliableRequest(1u, server.EndPoint, PrepareForConnectionRequest.pool.Obtain().Init(packet.userId, packet.userName, origin.endPoint, packet.random, packet.publicKey, isConnectionOwner, false));
                    SendReliableResponse(1u, origin.endPoint, packet, ConnectToServerResponse.pool.Obtain().InitForSuccess(server.UserId, server.Username, server.Secret, server.DiscoveryPolicy, server.InvitePolicy, server.MaxPlayerCount, server.Configuration, isConnectionOwner, false, server.EndPoint, server.Random, server.PublicKey));
                }
                else
                {
                    SendReliableResponse(1u, origin.endPoint, packet, ConnectToServerResponse.pool.Obtain().InitForFailure(ConnectToServerResponse.Result.InvalidPassword));
                }
            }
            else
            {
                SendReliableResponse(1u, origin.endPoint, packet, ConnectToServerResponse.pool.Obtain().InitForFailure(ConnectToServerResponse.Result.InvalidCode));
            }

            packet.Release();
        }
        #endregion SERVER JOIN BY CODE

        #region MATCHMAKING
        protected override void HandleConnectToMatchmakingRequest(ConnectToMatchmakingRequest packet, MessageOrigin origin)
        {

            packet.Release();
        }
        #endregion MATCHMAKING
    }
}
