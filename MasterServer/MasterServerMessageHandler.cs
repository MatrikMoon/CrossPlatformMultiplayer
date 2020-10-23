using LiteNetLib.Utils;
using System.Net;

namespace MasterServer
{
    class MasterServerMessageHandler : MessageHandler
    {
        public MasterServerMessageHandler(IMessageSender sender, PacketEncryptionLayer encryptionLayer) : base(sender, encryptionLayer) {
            RegisterUserMessageHandlers();
        }

        protected override bool ShouldHandleUserMessage(IUserMessage packet, MessageHandler.MessageOrigin origin)
        {
            return packet is IUserServerToClientMessage;
        }

        protected override bool ShouldHandleHandshakeMessage(IHandshakeMessage packet, MessageHandler.MessageOrigin origin)
        {
            return packet is IHandshakeServerToClientMessage;
        }
    }
}
