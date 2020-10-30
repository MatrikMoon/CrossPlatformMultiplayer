using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace MasterServer
{
    class MasterServerMessageHandler : Ripped.RippedMessageHandler
    {
        private Task<IDiffieHellmanKeyPair> _keysTask = DiffieHellmanUtility.GenerateKeysAsync(DiffieHellmanUtility.KeyType.ElipticalCurve);
        private PacketEncryptionLayer _encryptionLayer;

        private Dictionary<IPEndPoint, byte[]> serverRandoms = new Dictionary<IPEndPoint, byte[]>();
        private Dictionary<IPEndPoint, byte[]> clientRandoms = new Dictionary<IPEndPoint, byte[]>();
        private Dictionary<IPEndPoint, IDiffieHellmanKeyPair> serverKeys = new Dictionary<IPEndPoint, IDiffieHellmanKeyPair>();

        public MasterServerMessageHandler(IMessageSender sender, PacketEncryptionLayer encryptionLayer) : base(sender, encryptionLayer)
        {
            RegisterUserMessageHandlers();
            _encryptionLayer = encryptionLayer;
        }

        protected override bool ShouldHandleUserMessage(IUserMessage packet, MessageOrigin origin)
        {
            return packet is IUserClientToServerMessage;
        }

        protected override bool ShouldHandleHandshakeMessage(IHandshakeMessage packet, MessageOrigin origin)
        {
            return packet is IHandshakeClientToServerMessage;
        }

        protected override void HandleClientHelloRequest(ClientHelloRequest packet, MessageOrigin origin)
        {
            if (!clientRandoms.ContainsKey(origin.endPoint)) clientRandoms[origin.endPoint] = packet.random;

            var random = PacketEncryptionLayer.GenerateRandom(32);
            SendReliableResponse(1u, origin.endPoint, packet, HelloVerifyRequest.pool.Obtain().Init(random));
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

            var a = preMasterSecret;
            var b = serverRandoms[origin.endPoint];
            var c = clientRandoms[origin.endPoint];
            var d = serverKeys[origin.endPoint].publicKey;
            var e = packet.clientPublicKey;

            SendReliableResponse(1u, origin.endPoint, packet, ChangeCipherSpecRequest.pool.Obtain());

            _encryptionLayer.AddEncryptedEndpoint(1u, origin.endPoint, null, null, preMasterSecret, serverRandoms[origin.endPoint], clientRandoms[origin.endPoint], false);

            packet.Release();
        }
    }
}
