﻿using Coop.Network;
using Stateless;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Multiplayer
{
    public class ConnectionClient : ConnectionBase
    {
        private enum ETrigger
        {
            TryJoinServer,
            ServerAcceptedJoinRequest,
            ConnectionEstablished,
            Disconnect,
            Disconnected
        }
        private readonly StateMachine<EConnectionState, ETrigger> m_StateMachine;
        public override EConnectionState State => m_StateMachine.State;

        public ConnectionClient(INetworkConnection network)
            : base(network)
        {
            m_StateMachine = new StateMachine<EConnectionState, ETrigger>(EConnectionState.Disconnected);

            m_StateMachine.Configure(EConnectionState.Disconnected)
                .Permit(ETrigger.TryJoinServer, EConnectionState.ClientJoinRequesting);

            var disconnectTrigger = m_StateMachine.SetTriggerParameters<EDisconnectReason>(ETrigger.Disconnect);
            m_StateMachine.Configure(EConnectionState.Disconnecting)
                .OnEntryFrom(disconnectTrigger, eReason => closeConnection(eReason))
                .Permit(ETrigger.Disconnected, EConnectionState.Disconnected);

            m_StateMachine.Configure(EConnectionState.ClientJoinRequesting)
                .OnEntry(() => sendClientHello())
                .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting)
                .Permit(ETrigger.ServerAcceptedJoinRequest, EConnectionState.ClientJoining);

            m_StateMachine.Configure(EConnectionState.ClientJoining)
                .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting)
                .Permit(ETrigger.ConnectionEstablished, EConnectionState.ClientConnected);

            m_StateMachine.Configure(EConnectionState.ClientConnected)
                .Permit(ETrigger.Disconnect, EConnectionState.Disconnecting);

            Dispatcher.RegisterPacketHandlers(this);
        }
        ~ConnectionClient()
        {
            Dispatcher.UnregisterPacketHandlers(this);
        }
        public void Connect()
        {
            m_StateMachine.Fire(ETrigger.TryJoinServer);
        }
        public override void Disconnect(EDisconnectReason eReason)
        {
            if (!m_StateMachine.IsInState(EConnectionState.Disconnected))
            {
                m_StateMachine.Fire(new StateMachine<EConnectionState, ETrigger>.TriggerWithParameters<EDisconnectReason>(ETrigger.Disconnect), eReason);
            }
        }

        private void closeConnection(EDisconnectReason eReason)
        {
            m_Network.Close(eReason);
            m_StateMachine.Fire(ETrigger.Disconnected);
        }
        #region ClientJoinRequesting
        private void sendClientHello()
        {
            Send(new Packet(Protocol.EPacket.Client_Hello, new Protocol.Client_Hello(Protocol.Version).Serialize()));
        }
        [PacketHandler(EConnectionState.ClientJoinRequesting, Protocol.EPacket.Server_RequestClientInfo)]
        private void receiveClientInfoRequest(Packet packet)
        {
            Protocol.Server_RequestClientInfo payload = Protocol.Server_RequestClientInfo.Deserialize(new ByteReader(packet.Payload));
            sendClientInfo();
        }
        private void sendClientInfo()
        {
            Send(new Packet(Protocol.EPacket.Client_Info, new Protocol.Client_Info(new Player("Unknown")).Serialize()));
        }
        [PacketHandler(EConnectionState.ClientJoinRequesting, Protocol.EPacket.Server_JoinRequestAccepted)]
        private void receiveJoinRequestAccepted(Packet packet)
        {
            Protocol.Server_JoinRequestAccepted payload = Protocol.Server_JoinRequestAccepted.Deserialize(new ByteReader(packet.Payload));
            m_StateMachine.Fire(ETrigger.ServerAcceptedJoinRequest);
        }
        #endregion
        [PacketHandler(EConnectionState.ClientJoining, Protocol.EPacket.Server_KeepAlive)]
        [PacketHandler(EConnectionState.ClientConnected, Protocol.EPacket.Server_KeepAlive)]
        private void receiveServerKeepAlive(Packet packet)
        {
            Protocol.Server_KeepAlive payload = Protocol.Server_KeepAlive.Deserialize(new ByteReader(packet.Payload));
            Send(new Packet(Protocol.EPacket.Client_KeepAlive, new Protocol.Client_KeepAlive(payload.m_iKeepAliveID).Serialize()));
        }
    }
}