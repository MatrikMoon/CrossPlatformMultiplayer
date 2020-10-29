using System.Collections.Generic;

namespace MasterServer
{
    class MasterServerMessageHandler : MessageHandler
    {
        public MasterServerMessageHandler(IMessageSender sender, PacketEncryptionLayer encryptionLayer) : base(sender, encryptionLayer) {

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
            SendReliableResponse(1u, origin.endPoint, packet, HelloVerifyRequest.pool.Obtain().Init(packet.random));
            packet.Release();
        }

        protected override void HandleClientHelloWithCookieRequest(ClientHelloWithCookieRequest packet, MessageOrigin origin)
        {
            var random = PacketEncryptionLayer.GenerateRandom(128);
            SendReliableResponse(1u, origin.endPoint, packet, ServerHelloRequest.pool.Obtain().Init(packet.random, random, random));
            SendReliableResponse(1u, origin.endPoint, packet.certificateResponseId, ServerCertificateRequest.pool.Obtain().Init(new List<byte[]>
            {
                random
            }));
            packet.Release();
        }
    }
}
