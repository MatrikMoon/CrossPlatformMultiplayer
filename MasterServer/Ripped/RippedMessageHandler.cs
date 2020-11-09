using LiteNetLib.Utils;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServer.Ripped
{
    public class RippedMessageHandler : IDisposable
	{
		public RippedMessageHandler(IMessageSender sender, PacketEncryptionLayer encryptionLayer, IAnalyticsManager analytics = null)
		{
			RegisterHandshakeMessageHandlers();
			_sender = sender;
			this.encryptionLayer = encryptionLayer;
			this.encryptionLayer.SetUnencryptedTrafficFilter(CreateHandshakeHeader());
		}

		private void RegisterHandshakeMessageHandlers()
		{
			_handshakeMessageSerializer.RegisterCallback(HandshakeMessageType.ClientHelloRequest, new Action<ClientHelloRequest, MessageOrigin>(HandleClientHelloRequest), new Func<ClientHelloRequest>(ClientHelloRequest.pool.Obtain));
			_handshakeMessageSerializer.RegisterCallback(HandshakeMessageType.HelloVerifyRequest, new Action<HelloVerifyRequest, MessageOrigin>(HandleHelloVerifyRequest), new Func<HelloVerifyRequest>(HelloVerifyRequest.pool.Obtain));
			_handshakeMessageSerializer.RegisterCallback(HandshakeMessageType.ClientHelloWithCookieRequest, CustomResponseHandler(new Action<ClientHelloWithCookieRequest, MessageOrigin>(HandleClientHelloWithCookieRequest)), new Func<ClientHelloWithCookieRequest>(ClientHelloWithCookieRequest.pool.Obtain));
			_handshakeMessageSerializer.RegisterCallback(HandshakeMessageType.ServerHelloRequest, new Action<ServerHelloRequest, MessageOrigin>(DefaultResponseHandler), new Func<ServerHelloRequest>(ServerHelloRequest.pool.Obtain));
			_handshakeMessageSerializer.RegisterCallback(HandshakeMessageType.ServerCertificateRequest, new Action<ServerCertificateRequest, MessageOrigin>(DefaultResponseHandler), new Func<ServerCertificateRequest>(ServerCertificateRequest.pool.Obtain));
			_handshakeMessageSerializer.RegisterCallback(HandshakeMessageType.ClientKeyExchangeRequest, CustomResponseHandler(new Action<ClientKeyExchangeRequest, MessageOrigin>(HandleClientKeyExchangeRequest)), new Func<ClientKeyExchangeRequest>(ClientKeyExchangeRequest.pool.Obtain));
			_handshakeMessageSerializer.RegisterCallback(HandshakeMessageType.ChangeCipherSpecRequest, new Action<ChangeCipherSpecRequest, MessageOrigin>(DefaultResponseHandler), new Func<ChangeCipherSpecRequest>(ChangeCipherSpecRequest.pool.Obtain));
			_handshakeMessageSerializer.RegisterCallback(HandshakeMessageType.MessageReceivedAcknowledge, new Action<HandshakeMessageReceivedAcknowledge, MessageOrigin>(DefaultAcknowledgeHandler), new Func<HandshakeMessageReceivedAcknowledge>(HandshakeMessageReceivedAcknowledge.pool.Obtain));
			_handshakeMessageSerializer.RegisterCallback(HandshakeMessageType.MultipartMessage, new Action<HandshakeMultipartMessage, MessageOrigin>(DefaultMultipartMessageHandler), new Func<HandshakeMultipartMessage>(HandshakeMultipartMessage.pool.Obtain));
		}

		protected void RegisterUserMessageHandlers()
		{
			_userMessageSerializer.RegisterCallback(UserMessageType.AuthenticateUserRequest, CustomResponseHandler(new Action<AuthenticateUserRequest, MessageOrigin>(HandleAuthenticateUserRequest)), new Func<AuthenticateUserRequest>(AuthenticateUserRequest.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.AuthenticateUserResponse, new Action<AuthenticateUserResponse, MessageOrigin>(DefaultResponseHandler), new Func<AuthenticateUserResponse>(AuthenticateUserResponse.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.BroadcastServerStatusRequest, CustomResponseHandler(new Action<BroadcastServerStatusRequest, MessageOrigin>(HandleBroadcastServerStatusRequest)), new Func<BroadcastServerStatusRequest>(BroadcastServerStatusRequest.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.BroadcastServerStatusResponse, new Action<BroadcastServerStatusResponse, MessageOrigin>(DefaultResponseHandler), new Func<BroadcastServerStatusResponse>(BroadcastServerStatusResponse.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.BroadcastServerHeartbeatRequest, CustomUnreliableResponseHandler(new Action<BroadcastServerHeartbeatRequest, MessageOrigin>(HandleBroadcastServerHeartbeatRequest)), new Func<BroadcastServerHeartbeatRequest>(BroadcastServerHeartbeatRequest.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.BroadcastServerHeartbeatResponse, CustomUnreliableResponseHandler(new Action<BroadcastServerHeartbeatResponse, MessageOrigin>(HandleBroadcastServerHeartbeatResponse)), new Func<BroadcastServerHeartbeatResponse>(BroadcastServerHeartbeatResponse.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.BroadcastServerRemoveRequest, CustomUnreliableResponseHandler(new Action<BroadcastServerRemoveRequest, MessageOrigin>(HandleBroadcastServerRemoveRequest)), new Func<BroadcastServerRemoveRequest>(BroadcastServerRemoveRequest.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.ConnectToServerRequest, CustomResponseHandler(new Action<ConnectToServerRequest, MessageOrigin>(HandleConnectToServerRequest)), new Func<ConnectToServerRequest>(ConnectToServerRequest.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.ConnectToServerResponse, new Action<ConnectToServerResponse, MessageOrigin>(DefaultResponseHandler), new Func<ConnectToServerResponse>(ConnectToServerResponse.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.ConnectToMatchmakingRequest, CustomResponseHandler(new Action<ConnectToMatchmakingRequest, MessageOrigin>(HandleConnectToMatchmakingRequest)), new Func<ConnectToMatchmakingRequest>(ConnectToMatchmakingRequest.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.PrepareForConnectionRequest, CustomResponseHandler(new Action<PrepareForConnectionRequest, MessageOrigin>(HandlePrepareForConnectionRequest)), new Func<PrepareForConnectionRequest>(PrepareForConnectionRequest.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.GetPublicServersRequest, CustomResponseHandler(new Action<GetPublicServersRequest, MessageOrigin>(HandleGetPublicServersRequest)), new Func<GetPublicServersRequest>(GetPublicServersRequest.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.GetPublicServersResponse, new Action<GetPublicServersResponse, MessageOrigin>(DefaultResponseHandler), new Func<GetPublicServersResponse>(GetPublicServersResponse.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.MessageReceivedAcknowledge, new Action<UserMessageReceivedAcknowledge, MessageOrigin>(DefaultAcknowledgeHandler), new Func<UserMessageReceivedAcknowledge>(UserMessageReceivedAcknowledge.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.MultipartMessage, new Action<UserMultipartMessage, MessageOrigin>(DefaultMultipartMessageHandler), new Func<UserMultipartMessage>(UserMultipartMessage.pool.Obtain));
			_userMessageSerializer.RegisterCallback(UserMessageType.SessionKeepaliveMessage, new Action<SessionKeepaliveMessage, MessageOrigin>(HandleSessionKeepaliveMessage), new Func<SessionKeepaliveMessage>(SessionKeepaliveMessage.pool.Obtain));
		}

		protected virtual void HandleClientKeyExchangeRequest(ClientKeyExchangeRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleAuthenticateUserRequest(AuthenticateUserRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		#region RIPPED
		protected virtual bool ShouldHandleHandshakeMessage(IHandshakeMessage packet, MessageOrigin origin)
		{
			return false;
		}

		protected virtual void HandleClientHelloRequest(ClientHelloRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected void HandleHelloVerifyRequest(HelloVerifyRequest packet, MessageOrigin origin)
		{
			if (!ShouldHandleHandshakeMessage(packet, origin))
			{
				packet.Release();
			}
			if (!CompleteRequest(packet, origin.endPoint))
			{
				packet.Release();
			}
		}

		protected virtual void HandleClientHelloWithCookieRequest(ClientHelloWithCookieRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual bool ShouldHandleUserMessage(IUserMessage packet, MessageOrigin origin)
		{
			return false;
		}

		protected virtual void HandleBroadcastServerStatusRequest(BroadcastServerStatusRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleBroadcastServerHeartbeatRequest(BroadcastServerHeartbeatRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleBroadcastServerHeartbeatResponse(BroadcastServerHeartbeatResponse packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleBroadcastServerRemoveRequest(BroadcastServerRemoveRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleConnectToServerRequest(ConnectToServerRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleConnectToMatchmakingRequest(ConnectToMatchmakingRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandlePrepareForConnectionRequest(PrepareForConnectionRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleGetPublicServersRequest(GetPublicServersRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		private void HandleSessionKeepaliveMessage(SessionKeepaliveMessage packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected void RegisterDedicatedServerHandlers()
		{
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.AuthenticateDedicatedServerRequest, new Action<AuthenticateDedicatedServerRequest, MessageOrigin>(DefaultResponseHandler), new Func<AuthenticateDedicatedServerRequest>(AuthenticateDedicatedServerRequest.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.AuthenticateDedicatedServerResponse, new Action<AuthenticateDedicatedServerResponse, MessageOrigin>(DefaultResponseHandler), new Func<AuthenticateDedicatedServerResponse>(AuthenticateDedicatedServerResponse.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.GetAvailableRelayServerRequest, CustomResponseHandler(new Action<GetAvailableRelayServerRequest, MessageOrigin>(HandleGetAvailableRelayServerRequest)), new Func<GetAvailableRelayServerRequest>(GetAvailableRelayServerRequest.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.GetAvailableRelayServerResponse, new Action<GetAvailableRelayServerResponse, MessageOrigin>(DefaultResponseHandler), new Func<GetAvailableRelayServerResponse>(GetAvailableRelayServerResponse.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.GetAvailableMatchmakingServerRequest, CustomResponseHandler(new Action<GetAvailableMatchmakingServerRequest, MessageOrigin>(HandleGetAvailableMatchmakingServerRequest)), new Func<GetAvailableMatchmakingServerRequest>(GetAvailableMatchmakingServerRequest.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.GetAvailableMatchmakingServerResponse, new Action<GetAvailableMatchmakingServerResponse, MessageOrigin>(DefaultResponseHandler), new Func<GetAvailableMatchmakingServerResponse>(GetAvailableMatchmakingServerResponse.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.DedicatedServerUnavailableRequest, CustomResponseHandler(new Action<DedicatedServerNoLongerOccupiedRequest, MessageOrigin>(HandleDedicatedServerUnavailableRequest)), new Func<DedicatedServerNoLongerOccupiedRequest>(DedicatedServerNoLongerOccupiedRequest.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.DedicatedServerHeartbeatRequest, CustomUnreliableResponseHandler(new Action<DedicatedServerHeartbeatRequest, MessageOrigin>(HandleDedicatedServerHeartbeatRequest)), new Func<DedicatedServerHeartbeatRequest>(DedicatedServerHeartbeatRequest.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.DedicatedServerHeartbeatResponse, CustomUnreliableResponseHandler(new Action<DedicatedServerHeartbeatResponse, MessageOrigin>(HandleDedicatedServerHeartbeatResponse)), new Func<DedicatedServerHeartbeatResponse>(DedicatedServerHeartbeatResponse.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.RelayServerStatusUpdateRequest, CustomResponseHandler(new Action<RelayServerStatusUpdateRequest, MessageOrigin>(HandleRelayServerStatusUpdateRequest)), new Func<RelayServerStatusUpdateRequest>(RelayServerStatusUpdateRequest.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.MatchmakingServerStatusUpdateRequest, CustomResponseHandler(new Action<MatchmakingServerStatusUpdateRequest, MessageOrigin>(HandleMatchmakingServerStatusUpdateRequest)), new Func<MatchmakingServerStatusUpdateRequest>(MatchmakingServerStatusUpdateRequest.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.DedicatedServerShutDownRequest, CustomUnreliableResponseHandler(new Action<DedicatedServerShutDownRequest, MessageOrigin>(HandleDedicatedServerShutDownRequest)), new Func<DedicatedServerShutDownRequest>(DedicatedServerShutDownRequest.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.DedicatedServerPrepareForConnectionRequest, CustomResponseHandler(new Action<DedicatedServerPrepareForConnectionRequest, MessageOrigin>(HandleDedicatedServerPrepareForConnectionRequest)), new Func<DedicatedServerPrepareForConnectionRequest>(DedicatedServerPrepareForConnectionRequest.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.MessageReceivedAcknowledge, new Action<DedicatedServerMessageReceivedAcknowledge, MessageOrigin>(DefaultAcknowledgeHandler), new Func<DedicatedServerMessageReceivedAcknowledge>(DedicatedServerMessageReceivedAcknowledge.pool.Obtain));
			_dedicatedServerMessageSerializer.RegisterCallback(DedicatedServerMessageType.MultipartMessage, new Action<DedicatedServerMultipartMessage, MessageOrigin>(DefaultMultipartMessageHandler), new Func<DedicatedServerMultipartMessage>(DedicatedServerMultipartMessage.pool.Obtain));
		}

		protected virtual bool ShouldHandleDedicatedServerMessage(IDedicatedServerMessage packet, MessageOrigin origin)
		{
			return false;
		}

		protected virtual void HandleGetAvailableRelayServerRequest(GetAvailableRelayServerRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleGetAvailableMatchmakingServerRequest(GetAvailableMatchmakingServerRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleDedicatedServerUnavailableRequest(DedicatedServerNoLongerOccupiedRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleDedicatedServerHeartbeatRequest(DedicatedServerHeartbeatRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleDedicatedServerHeartbeatResponse(DedicatedServerHeartbeatResponse packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleRelayServerStatusUpdateRequest(RelayServerStatusUpdateRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleMatchmakingServerStatusUpdateRequest(MatchmakingServerStatusUpdateRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleDedicatedServerShutDownRequest(DedicatedServerShutDownRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		protected virtual void HandleDedicatedServerPrepareForConnectionRequest(DedicatedServerPrepareForConnectionRequest packet, MessageOrigin origin)
		{
			packet.Release();
		}

		private void DefaultAcknowledgeHandler<T>(T packet, MessageOrigin origin) where T : IMasterServerMessage, IMasterServerAcknowledgeMessage
		{
			CompleteSend(packet, origin.endPoint);
			packet.Release();
		}

		private void DefaultResponseHandler<T>(T packet, MessageOrigin origin) where T : IMasterServerReliableResponse
		{
			if (IsUnhandledMessage(packet, origin))
			{
				packet.Release();
			}
		}

		private void DefaultMultipartMessageHandler<T>(T packet, MessageOrigin origin) where T : IMasterServerMultipartMessage
		{
			if (!IsUnhandledMessage(packet, origin))
			{
				return;
			}
            RequestWaiterId key = new RequestWaiterId(origin.endPoint, packet.multipartMessageId);
            MultipartMessageWaiter multipartMessageWaiter = _multipartMessageBuffer.Get(key);
			if (multipartMessageWaiter == null)
			{
				multipartMessageWaiter = new MultipartMessageWaiter(_bufferPool);
				_multipartMessageBuffer.Push(key, multipartMessageWaiter);
			}
			multipartMessageWaiter.Append(packet);
			packet.Release();
			if (multipartMessageWaiter.isWaiting)
			{
				return;
			}
			_multipartReader.SetSource(multipartMessageWaiter.data, 0, multipartMessageWaiter.length);
			ReceiveMessage(origin.endPoint, _multipartReader);
			multipartMessageWaiter.Dispose();
		}

		private Action<T, MessageOrigin> CustomResponseHandler<T>(Action<T, MessageOrigin> customHandler) where T : IMasterServerReliableRequest
		{
			return delegate (T packet, MessageOrigin origin)
			{
				if (IsUnhandledMessage(packet, origin))
				{
					customHandler(packet, origin);
				}
			};
		}

		private Action<T, MessageOrigin> CustomUnreliableResponseHandler<T>(Action<T, MessageOrigin> customHandler) where T : IMasterServerUnreliableMessage
		{
			return delegate (T packet, MessageOrigin origin)
			{
				if (!ShouldHandleMessage(packet, origin))
				{
					packet.Release();
					return;
				}
				customHandler(packet, origin);
			};
		}

		private bool IsNewRequest(IMasterServerReliableRequest packet, IPEndPoint remoteEndPoint)
		{
			if (_receivedBuffer.Push(new RequestWaiterId(remoteEndPoint, packet.requestId), null))
			{
				return true;
			}
			packet.Release();
			return false;
		}

		private bool IsUnhandledMessage(IMasterServerReliableRequest packet, MessageOrigin origin)
		{
			Logger.Debug($"IsUnhandledMessage ({packet.GetType()})");

			bool flag = ShouldHandleMessage(packet, origin);
			if (packet is IHandshakeMessage)
			{
				SendUnreliableResponse(origin.protocolVersion, origin.endPoint, packet.requestId, HandshakeMessageReceivedAcknowledge.pool.Obtain().Init(flag));
			}
			else if (packet is IUserMessage)
			{
				SendUnreliableResponse(origin.protocolVersion, origin.endPoint, packet.requestId, UserMessageReceivedAcknowledge.pool.Obtain().Init(flag));
			}
			else if (packet is IDedicatedServerMessage)
			{
				SendUnreliableResponse(origin.protocolVersion, origin.endPoint, packet.requestId, DedicatedServerMessageReceivedAcknowledge.pool.Obtain().Init(flag));
			}
			if (!flag)
			{
				packet.Release();
				return false;
			}
			if (!IsNewRequest(packet, origin.endPoint))
			{
				return false;
			}
			IMasterServerReliableResponse masterServerReliableResponse;
			if ((masterServerReliableResponse = (packet as IMasterServerReliableResponse)) != null)
			{
				return !CompleteRequest(masterServerReliableResponse, origin.endPoint);
			}
			return true;
		}

		private void CompleteSend(IMasterServerResponse packet, IPEndPoint remoteEndPoint)
		{
            SentRequestWaiter sentRequestWaiter = _sentRequestWaiters.Get(new RequestWaiterId(remoteEndPoint, packet.responseId));
			IMasterServerAcknowledgeMessage masterServerAcknowledgeMessage;
			if ((masterServerAcknowledgeMessage = (packet as IMasterServerAcknowledgeMessage)) != null)
			{
				if (sentRequestWaiter != null)
				{
					sentRequestWaiter.Complete(masterServerAcknowledgeMessage.messageHandled);
					return;
				}
			}
			else if (sentRequestWaiter != null)
			{
				sentRequestWaiter.Complete(true);
			}
		}

		private bool CompleteRequest(IMasterServerReliableResponse packet, IPEndPoint remoteEndPoint)
		{
            RequestResponseWaiter requestResponseWaiter = _requestResponseWaiters.Get(new RequestWaiterId(remoteEndPoint, packet.responseId));
			if (requestResponseWaiter != null)
			{
				CompleteSend(packet, remoteEndPoint);
				requestResponseWaiter.Complete(packet);
				return true;
			}
			return false;
		}

		private bool ShouldHandleMessage(IMasterServerMessage packet, MessageOrigin origin)
		{
			if (packet is IMasterServerMultipartMessage)
			{
				return true;
			}
			IHandshakeMessage packet2;
			if ((packet2 = (packet as IHandshakeMessage)) != null)
			{
				return ShouldHandleHandshakeMessage(packet2, origin);
			}
			IUserMessage packet3;
			if ((packet3 = (packet as IUserMessage)) != null)
			{
				return ShouldHandleUserMessage(packet3, origin);
			}
			IDedicatedServerMessage packet4;
			return (packet4 = (packet as IDedicatedServerMessage)) != null && ShouldHandleDedicatedServerMessage(packet4, origin);
		}

		protected async void GetAndSendResponse<TRequest, TResponse>(TRequest request, MessageOrigin origin, Func<TRequest, MessageOrigin, Task<TResponse>> tryGetResponse, Func<TResponse> getFailureResponse) where TRequest : IMasterServerReliableRequest where TResponse : IMasterServerReliableResponse
		{
			try
			{
				await GetAndSendResponseAsync(request, origin, tryGetResponse, getFailureResponse);
			}
			catch (Exception ex)
			{
				ReceivedMessageException(origin.endPoint, ex);
			}
		}

		protected async Task GetAndSendResponseAsync<TRequest, TResponse>(TRequest request, MessageOrigin origin, Func<TRequest, MessageOrigin, Task<TResponse>> tryGetResponse, Func<TResponse> getFailureResponse) where TRequest : IMasterServerReliableRequest where TResponse : IMasterServerReliableResponse
		{
			TResponse response = default(TResponse);
			try
			{
				TResponse tresponse = await tryGetResponse(request, origin);
				response = tresponse;
				if (response == null)
				{
					response = getFailureResponse();
				}
			}
			catch (Exception)
			{
				response = getFailureResponse();
				throw;
			}
			finally
			{
				SendReliableResponse(origin.protocolVersion, origin.endPoint, request, response, default(CancellationToken));
			}
		}

		protected async void GetAndSendUnreilableResponse<TRequest, TResponse>(TRequest request, MessageOrigin origin, Func<TRequest, MessageOrigin, Task<TResponse>> tryGetResponse, Func<TResponse> getFailureResponse) where TRequest : IMasterServerUnreliableMessage where TResponse : IMasterServerUnreliableMessage
		{
			TResponse response = default(TResponse);
			try
			{
				TResponse tresponse = await tryGetResponse(request, origin);
				response = tresponse;
				if (response == null)
				{
					response = getFailureResponse();
				}
			}
			catch (Exception)
			{
				response = getFailureResponse();
			}
			finally
			{
				request.Release();
				SendUnreliableMessage(origin.protocolVersion, origin.endPoint, response);
			}
		}

		protected void SendUnreliableMessage(uint protocolVersion, IPEndPoint remoteEndPoint, IMasterServerUnreliableMessage message)
		{
			SendMessage(protocolVersion, remoteEndPoint, message);
		}

		protected void SendUnreliableResponse(uint protocolVersion, IPEndPoint remoteEndPoint, IMasterServerReliableRequest request, IMasterServerResponse response)
		{
			uint requestId = request.requestId;
			request.Release();
			SendUnreliableResponse(protocolVersion, remoteEndPoint, requestId, response);
		}

		protected void SendUnreliableResponse(uint protocolVersion, IPEndPoint remoteEndPoint, uint responseId, IMasterServerResponse response)
		{
			SendMessage(protocolVersion, remoteEndPoint, response.WithResponseId(responseId));
		}

		protected void SendReliableRequest(uint protocolVersion, IPEndPoint remoteEndPoint, IMasterServerReliableRequest request, CancellationToken cancellationToken = default(CancellationToken))
		{
			SendMessageWithRetry(protocolVersion, remoteEndPoint, request.WithRequestId(GetNextRequestId()), cancellationToken);
		}

		protected Task SendReliableRequestAsync(uint protocolVersion, IPEndPoint remoteEndPoint, IMasterServerReliableRequest request, Func<uint, IPEndPoint, IMasterServerReliableRequest, CancellationToken, Task> onSendFailed = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return SendMessageWithRetryAsync(protocolVersion, remoteEndPoint, request.WithRequestId(GetNextRequestId()), onSendFailed, cancellationToken);
		}

		protected void SendReliableResponse(uint protocolVersion, IPEndPoint remoteEndPoint, IMasterServerReliableRequest request, IMasterServerReliableResponse response, CancellationToken cancellationToken = default(CancellationToken))
		{
			uint requestId = request.requestId;
			request.Release();
			SendReliableResponse(protocolVersion, remoteEndPoint, requestId, response, cancellationToken);
		}

		protected void SendReliableResponse(uint protocolVersion, IPEndPoint remoteEndPoint, uint responseId, IMasterServerReliableResponse response, CancellationToken cancellationToken = default(CancellationToken))
		{
			SendMessageWithRetry(protocolVersion, remoteEndPoint, response.WithRequestAndResponseId(GetNextRequestId(), responseId), cancellationToken);
		}

		protected Task SendReliableResponseAsync(uint protocolVersion, IPEndPoint remoteEndPoint, IMasterServerReliableRequest request, IMasterServerReliableResponse response, CancellationToken cancellationToken = default(CancellationToken))
		{
			uint requestId = request.requestId;
			request.Release();
			return SendMessageWithRetryAsync(protocolVersion, remoteEndPoint, response.WithRequestAndResponseId(GetNextRequestId(), requestId), null, cancellationToken);
		}

		private void SendMessage(uint protocolVersion, IPEndPoint remoteEndPoint, IMasterServerMessage message)
		{
			_sender.SendMessage(Write(protocolVersion, message), remoteEndPoint);
			message.Release();
		}

		private async void SendMessageWithRetry(uint protocolVersion, IPEndPoint remoteEndPoint, IMasterServerReliableRequest message, CancellationToken cancellationToken = default(CancellationToken))
		{
			try
			{
				await SendMessageWithRetryAsync(protocolVersion, remoteEndPoint, message, null, cancellationToken);
			}
			catch (TimeoutException)
			{
			}
			catch (TaskCanceledException)
			{
			}
			catch (Exception arg)
			{
				BGNetDebug.LogError("Exception thrown sending message " + arg);
			}
		}

		private Task SendMessageWithRetryAsync(uint protocolVersion, IPEndPoint remoteEndPoint, IMasterServerReliableRequest message, Func<uint, IPEndPoint, IMasterServerReliableRequest, CancellationToken, Task> onSendFailed = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			NetDataWriter netDataWriter = Write(protocolVersion, message);
			if (netDataWriter.Length > 412)
			{
				return SendMultipartMessageWithRetryAsync(protocolVersion, remoteEndPoint, message, netDataWriter, onSendFailed, cancellationToken);
			}
			return SendMessageWithRetryAsyncInternal(protocolVersion, remoteEndPoint, message, onSendFailed, cancellationToken);
		}

		private async Task SendMultipartMessageWithRetryAsync(uint protocolVersion, IPEndPoint remoteEndPoint, IMasterServerReliableRequest message, NetDataWriter data, Func<uint, IPEndPoint, IMasterServerReliableRequest, CancellationToken, Task> onSendFailed, CancellationToken cancellationToken)
		{
			List<IMasterServerReliableRequest> list = new List<IMasterServerReliableRequest>();
			uint nextRequestId = GetNextRequestId();
			for (int i = 0; i < data.Length; i += 384)
			{
				BaseMasterServerMultipartMessage baseMasterServerMultipartMessage = null;
				if (message is IHandshakeMessage)
				{
					baseMasterServerMultipartMessage = HandshakeMultipartMessage.pool.Obtain();
				}
				if (message is IUserMessage)
				{
					baseMasterServerMultipartMessage = UserMultipartMessage.pool.Obtain();
				}
				if (message is IDedicatedServerMessage)
				{
					baseMasterServerMultipartMessage = DedicatedServerMultipartMessage.pool.Obtain();
				}
				list.Add(baseMasterServerMultipartMessage.Init(nextRequestId, data.Data, i, Math.Min(384, data.Length - i), data.Length));
			}
			bool shouldReleaseMessage = true;
			try
			{
				try
				{
					await Task.WhenAll(from mm in list
									   select SendMessageWithRetryAsyncInternal(protocolVersion, remoteEndPoint, mm.WithRequestId(GetNextRequestId()), null, cancellationToken));
				}
				catch (TimeoutException obj)
				{
					if (onSendFailed != null)
					{
						shouldReleaseMessage = false;
						await onSendFailed(protocolVersion, remoteEndPoint, message, cancellationToken);
					}
					else
					{
						Exception ex = obj;
						if (ex == null)
						{
							throw obj;
						}
						ExceptionDispatchInfo.Capture(ex).Throw();
					}
				}
			}
			finally
			{
				if (shouldReleaseMessage)
				{
					message.Release();
				}
			}
		}

		private async Task SendMessageWithRetryAsyncInternal(uint protocolVersion, IPEndPoint remoteEndPoint, IMasterServerReliableRequest message, Func<uint, IPEndPoint, IMasterServerReliableRequest, CancellationToken, Task> onSendFailed = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			SentRequestWaiter sentRequest = new SentRequestWaiter(_disposedTokenSource.Token, cancellationToken);
			_sentRequestWaiters.Push(new RequestWaiterId(remoteEndPoint, message.requestId), sentRequest);
			bool shouldReleaseMessage = true;
			try
			{
				try
				{
					int i = 0;
					while (i <= 5 && sentRequest.isWaiting)
					{
						_sender.SendMessage(Write(protocolVersion, message), remoteEndPoint);
						await Task.WhenAny(new Task[]
						{
							sentRequest.task,
							WaitForRetry(i, cancellationToken)
						});
						i++;
					}
					if (sentRequest.isWaiting)
					{
						_sender.SendMessage(Write(protocolVersion, message), remoteEndPoint);
						await sentRequest.task;
					}
				}
				catch (TaskCanceledException)
				{
					sentRequest.Cancel();
					throw;
				}
				catch (TimeoutException obj)
				{
					if (onSendFailed != null)
					{
						shouldReleaseMessage = false;
						await onSendFailed(protocolVersion, remoteEndPoint, message, cancellationToken);
					}
					else
					{
						Exception ex = obj;
						if (ex == null)
						{
							throw obj;
						}
						ExceptionDispatchInfo.Capture(ex).Throw();
					}
				}
			}
			finally
			{
				if (shouldReleaseMessage)
				{
					message.Release();
				}
			}
		}

		protected async Task<T> AwaitResponseAsync<T>(uint protocolVersion, IPEndPoint remoteEndPoint, uint requestId, CancellationToken cancellationToken = default(CancellationToken)) where T : IMasterServerReliableResponse
		{
			RequestResponseWaiter requestResponseWaiter = new RequestResponseWaiter(_disposedTokenSource.Token, cancellationToken);
			_requestResponseWaiters.Push(new RequestWaiterId(remoteEndPoint, requestId), requestResponseWaiter);
			IMasterServerMessage masterServerMessage = await requestResponseWaiter.task;
			IMasterServerMessage masterServerMessage2 = masterServerMessage;
			if (masterServerMessage2 is T)
			{
				return (T)masterServerMessage2;
			}
			if (masterServerMessage != null)
			{
				masterServerMessage.Release();
			}
			throw new Exception("Received Unexpected response");
		}

		private Task WaitForRetry(int retryAttempt, CancellationToken cancellationToken)
		{
			int millisecondsDelay;
			switch (retryAttempt)
			{
				case 0:
					millisecondsDelay = 200;
					break;
				case 1:
					millisecondsDelay = 300;
					break;
				case 2:
					millisecondsDelay = 450;
					break;
				case 3:
					millisecondsDelay = 600;
					break;
				case 4:
					millisecondsDelay = 1000;
					break;
				default:
					return Task.CompletedTask;
			}
			return Task.Delay(millisecondsDelay, cancellationToken);
		}

		private NetDataWriter Write(uint protocolVersion, INetSerializable message)
		{
			_dataWriter.Reset();
			uint num;
			if (message is IHandshakeMessage)
			{
				num = 3192347326u;
			}
			else if (message is IUserMessage)
			{
				num = 1u;
			}
			else
			{
				if (!(message is IDedicatedServerMessage))
				{
					throw new Exception(string.Format("Cannot write message of unknown type: {0}", message));
				}
				num = 2u;
			}
			INetworkPacketSerializer<MessageOrigin> serializer = GetSerializer(protocolVersion, num);
			_dataWriter.Put(num);
			_dataWriter.PutVarUInt(protocolVersion);
			serializer.SerializePacket(_dataWriter, message);
			byte[] data = _dataWriter.Data;
			return _dataWriter;
		}

		public virtual void PollUpdate()
		{
			_requestResponseWaiters.PollUpdate();
			_sentRequestWaiters.PollUpdate();
			_receivedBuffer.PollUpdate();
		}

		public void ReceiveMessage(IPEndPoint remoteEndPoint, NetDataReader reader)
		{
			if (!ShouldHandleMessageFromEndPoint(remoteEndPoint))
			{
				return;
			}
			uint messageType;
			uint protocolVersion;
			if (!reader.TryGetUInt(out messageType) || !reader.TryGetVarUInt(out protocolVersion))
			{
				Logger.Error($"Failed to read message type: {messageType}");
				return;
			}
			try
			{
				INetworkPacketSerializer<MessageOrigin> serializer = GetSerializer(protocolVersion, messageType);
				MessageOrigin data = new MessageOrigin(remoteEndPoint, protocolVersion);
				serializer.ProcessAllPackets(reader, data);
			}
			catch (Exception ex)
			{
				BGNetDebug.LogError("Exception thrown on message: " + ex);
				ReceivedMessageException(remoteEndPoint, ex);
			}
		}

		protected virtual bool ShouldHandleMessageFromEndPoint(IPEndPoint endPoint)
		{
			return true;
		}

		protected virtual void ReceivedMessageException(IPEndPoint endPoint, Exception ex)
		{
		}

		protected void ResetEpoch()
		{
			_lastRequestId ^= (uint)PacketEncryptionLayer.GetRandomByte() << 24;
		}

		protected uint GetNextRequestId()
		{
			if ((_lastRequestId ^ 16777215u) == 0u)
			{
				_lastRequestId ^= 16777215u;
			}
			_lastRequestId += 1u;
			return _lastRequestId;
		}

		private INetworkPacketSerializer<MessageOrigin> GetSerializer(uint protocolVersion, uint messageType)
		{
			if (protocolVersion != 1u)
			{
				throw new Exception(string.Format("Unknown Protocol Version {0}", protocolVersion));
			}
			if (messageType == 1u)
			{
				return _userMessageSerializer;
			}
			if (messageType == 2u)
			{
				return _dedicatedServerMessageSerializer;
			}
			if (messageType == 3192347326u)
			{
				return _handshakeMessageSerializer;
			}
			throw new Exception(string.Format("Unknown Message Type {0}", messageType));
		}

		public virtual void Dispose()
		{
			_multipartMessageBuffer.Dispose();
			_receivedBuffer.Dispose();
			_sentRequestWaiters.Dispose();
			_requestResponseWaiters.Dispose();
			_disposedTokenSource.Cancel();
		}

		private static byte[] CreateHandshakeHeader()
		{
			byte[] array = new byte[5];
			array[0] = 8;
			FastBitConverter.GetBytes(array, 1, 3192347326u);
			return array;
		}

		private readonly NetworkPacketSerializer<HandshakeMessageType, MessageOrigin> _handshakeMessageSerializer = new NetworkPacketSerializer<HandshakeMessageType, MessageOrigin>();

		private readonly NetworkPacketSerializer<UserMessageType, MessageOrigin> _userMessageSerializer = new NetworkPacketSerializer<UserMessageType, MessageOrigin>();

		private readonly NetworkPacketSerializer<DedicatedServerMessageType, MessageOrigin> _dedicatedServerMessageSerializer = new NetworkPacketSerializer<DedicatedServerMessageType, MessageOrigin>();

		private readonly NetDataWriter _dataWriter = new NetDataWriter();

		private readonly NetDataReader _multipartReader = new NetDataReader();

		protected readonly PacketEncryptionLayer encryptionLayer;

		private readonly IMessageSender _sender;

		private uint _lastRequestId;

		private readonly TimedCircularBuffer<RequestWaiterId, SentRequestWaiter> _sentRequestWaiters = new TimedCircularBuffer<RequestWaiterId, SentRequestWaiter>(5f, 32);

		private readonly TimedCircularBuffer<RequestWaiterId, RequestResponseWaiter> _requestResponseWaiters = new TimedCircularBuffer<RequestWaiterId, RequestResponseWaiter>(15f, 32);

		private readonly TimedCircularBuffer<RequestWaiterId, IDisposable> _receivedBuffer = new TimedCircularBuffer<RequestWaiterId, IDisposable>(120f, 32);

		private readonly TimedCircularBuffer<RequestWaiterId, MultipartMessageWaiter> _multipartMessageBuffer = new TimedCircularBuffer<RequestWaiterId, MultipartMessageWaiter>(10f, 32);

		private readonly SmallBufferPool _bufferPool = new SmallBufferPool();

		private readonly CancellationTokenSource _disposedTokenSource = new CancellationTokenSource();

		public interface IMessageSender
		{
			void SendMessage(NetDataWriter writer, IPEndPoint endPoint);
		}

		private struct RequestWaiterId : IEquatable<RequestWaiterId>
		{
			public RequestWaiterId(IPEndPoint endPoint, uint requestId)
			{
				this.endPoint = endPoint;
				this.requestId = requestId;
			}

			public bool Equals(RequestWaiterId other)
			{
				return Equals(endPoint, other.endPoint) && requestId == other.requestId;
			}

			public override bool Equals(object other)
			{
				if (other is RequestWaiterId)
				{
					RequestWaiterId other2 = (RequestWaiterId)other;
					return Equals(other2);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return ((endPoint == null) ? 0 : endPoint.GetHashCode()) ^ (int)requestId;
			}

			public readonly IPEndPoint endPoint;

			public readonly uint requestId;
		}

		private abstract class RequestWaiter : IDisposable
		{
			public abstract void Dispose();
		}

		private sealed class SentRequestWaiter : RequestWaiter
		{
			public SentRequestWaiter(CancellationToken disposedCancellationToken, CancellationToken requestCancellationToken)
			{
				_disposedCancellationTokenRegistration = disposedCancellationToken.Register(new Action(Cancel));
				_requestCancellationTokenRegistration = requestCancellationToken.Register(new Action(Cancel));
			}

			public override void Dispose()
			{
				if (isWaiting)
				{
					_taskCompletionSource.TrySetException(new TimeoutException());
				}
				_disposedCancellationTokenRegistration.Dispose();
				_requestCancellationTokenRegistration.Dispose();
			}

			public void Complete(bool handled = true)
			{
				if (handled)
				{
					_taskCompletionSource.TrySetResult(true);
					return;
				}
				_taskCompletionSource.TrySetException(new Exception("Request unhandled by the remote endpoint"));
			}

			public void Cancel()
			{
				_taskCompletionSource.TrySetCanceled();
			}

			public Task task
			{
				get
				{
					return _taskCompletionSource.Task;
				}
			}

			public bool isWaiting
			{
				get
				{
					return !task.IsCompleted && !task.IsCanceled && !task.IsFaulted;
				}
			}

			private readonly TaskCompletionSource<bool> _taskCompletionSource = new TaskCompletionSource<bool>();

			private readonly CancellationTokenRegistration _disposedCancellationTokenRegistration;

			private readonly CancellationTokenRegistration _requestCancellationTokenRegistration;
		}

		private sealed class RequestResponseWaiter : RequestWaiter
		{
			public RequestResponseWaiter(CancellationToken disposedCancellationToken, CancellationToken requestCancellationToken)
			{
				_disposedCancellationTokenRegistration = disposedCancellationToken.Register(new Action(Cancel));
				_requestCancellationTokenRegistration = requestCancellationToken.Register(new Action(Cancel));
			}

			public override void Dispose()
			{
				if (isWaiting)
				{
					_taskCompletionSource.TrySetException(new TimeoutException());
				}
				_disposedCancellationTokenRegistration.Dispose();
				_requestCancellationTokenRegistration.Dispose();
			}

			public void Complete(IMasterServerMessage response)
			{
				if (!_taskCompletionSource.TrySetResult(response))
				{
					response.Release();
				}
			}

			public void Fail(Exception ex)
			{
				_taskCompletionSource.TrySetException(ex);
			}

			public void Cancel()
			{
				_taskCompletionSource.TrySetCanceled();
			}

			public Task<IMasterServerMessage> task
			{
				get
				{
					return _taskCompletionSource.Task;
				}
			}

			public bool isWaiting
			{
				get
				{
					return !task.IsCompleted && !task.IsCanceled && !task.IsFaulted;
				}
			}

			private readonly TaskCompletionSource<IMasterServerMessage> _taskCompletionSource = new TaskCompletionSource<IMasterServerMessage>();

			private readonly CancellationTokenRegistration _disposedCancellationTokenRegistration;

			private readonly CancellationTokenRegistration _requestCancellationTokenRegistration;
		}

		private sealed class MultipartMessageWaiter : RequestWaiter
		{
			public MultipartMessageWaiter(SmallBufferPool bufferPool)
			{
				_bufferPool = bufferPool;
			}

			public override void Dispose()
			{
				_isDisposed = true;
				if (_buffer != null)
				{
					_bufferPool.ReleaseBuffer(_buffer);
					_buffer = null;
				}
			}

			public void Append(IMasterServerMultipartMessage packet)
			{
				if (_isComplete || _isDisposed)
				{
					return;
				}
				if (_buffer == null)
				{
					_length = packet.totalLength;
					_buffer = _bufferPool.GetBuffer(_length);
				}
				Buffer.BlockCopy(packet.data, 0, _buffer, packet.offset, packet.length);
				bool flag = false;
				for (int i = 0; i < _ranges.Count; i += 2)
				{
					if (packet.offset <= _ranges[i])
					{
						if (packet.offset + packet.length >= _ranges[i])
						{
							int value = Math.Max(packet.offset + packet.length, _ranges[i + 1]);
							_ranges[i] = packet.offset;
							_ranges[i + 1] = value;
						}
						else
						{
							_ranges.Insert(i, packet.offset);
							_ranges.Insert(i + 1, packet.offset + packet.length);
						}
						flag = true;
						break;
					}
					if (packet.offset <= _ranges[i + 1])
					{
						int value2 = Math.Max(packet.offset + packet.length, _ranges[i + 1]);
						_ranges[i + 1] = value2;
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					_ranges.Add(packet.offset);
					_ranges.Add(packet.offset + packet.length);
				}
				if (_ranges.Count == 2 && _ranges[0] == 0 && _ranges[1] == _length)
				{
					_isComplete = true;
				}
			}

			public bool isWaiting
			{
				get
				{
					return !_isComplete && !_isDisposed;
				}
			}

			public byte[] data
			{
				get
				{
					return _buffer;
				}
			}

			public int length
			{
				get
				{
					return _length;
				}
			}

			private readonly SmallBufferPool _bufferPool;

			private byte[] _buffer;

			private int _length;

			private readonly List<int> _ranges = new List<int>();

			private bool _isComplete;

			private bool _isDisposed;
		}

		protected readonly struct MessageOrigin
		{
			public MessageOrigin(IPEndPoint endPoint, uint protocolVersion)
			{
				this.endPoint = endPoint;
				this.protocolVersion = protocolVersion;
			}

			public readonly IPEndPoint endPoint;

			public readonly uint protocolVersion;
		}

		private enum HandshakeMessageType
		{
			ClientHelloRequest,
			HelloVerifyRequest,
			ClientHelloWithCookieRequest,
			ServerHelloRequest,
			ServerCertificateRequest,
			ServerCertificateResponse,
			ClientKeyExchangeRequest,
			ChangeCipherSpecRequest,
			MessageReceivedAcknowledge,
			MultipartMessage
		}

		private enum UserMessageType
		{
			AuthenticateUserRequest,
			AuthenticateUserResponse,
			BroadcastServerStatusRequest,
			BroadcastServerStatusResponse,
			BroadcastServerHeartbeatRequest,
			BroadcastServerHeartbeatResponse,
			BroadcastServerRemoveRequest,
			ConnectToServerRequest,
			ConnectToServerResponse,
			ConnectToMatchmakingRequest,
			PrepareForConnectionRequest,
			GetPublicServersRequest,
			GetPublicServersResponse,
			MessageReceivedAcknowledge,
			MultipartMessage,
			SessionKeepaliveMessage
		}

		private enum DedicatedServerMessageType
		{
			AuthenticateDedicatedServerRequest,
			AuthenticateDedicatedServerResponse,
			GetAvailableRelayServerRequest,
			GetAvailableRelayServerResponse,
			GetAvailableMatchmakingServerRequest,
			GetAvailableMatchmakingServerResponse,
			DedicatedServerUnavailableRequest,
			DedicatedServerHeartbeatRequest,
			DedicatedServerHeartbeatResponse,
			RelayServerStatusUpdateRequest,
			MatchmakingServerStatusUpdateRequest,
			DedicatedServerShutDownRequest,
			DedicatedServerPrepareForConnectionRequest,
			MessageReceivedAcknowledge,
			MultipartMessage
		}
        #endregion RIPPED
    }
}
