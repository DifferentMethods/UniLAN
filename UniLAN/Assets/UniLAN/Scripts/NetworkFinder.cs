using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;

namespace UniLAN
{
    public class PeerInformation
    {
        public string hostname;
        public IPAddress ipAddress;
        public float lastSeen;
    }

    public class NetworkFinder : MonoBehaviour
    {
        /// <summary>
        /// The app identifier uniquely identifies this app.
        /// </summary>
        public string appId;
        /// <summary>
        /// The group identifier identifies groups within the app identifier.
        /// </summary>
        public string groupId;
        /// <summary>
        /// The discovery port used to find other peers.
        /// </summary>
        public int discoveryPort = 3279;
        /// <summary>
        /// The broadcast frequency in hertz. Eg 0.5f is 1 broadcast every 2 seconds.
        /// </summary>
        public float broadcastFrequency = 0.5f;
        /// <summary>
        /// If broadcast is true, the broadcast beacon is sent.
        /// </summary>
        public bool broadcast = true;
        /// <summary>
        /// The network mask of your network.
        /// </summary>
        public string networkMask = "255.255.255.0";
        /// <summary>
        /// Any peers that have not been seen for this period of time will be discarded.
        /// </summary>
        public float maxAge = 15;
        /// <summary>
        /// A list of discovered peers.
        /// </summary>
        public List<PeerInformation> peers = new List<PeerInformation> ();

        public event System.Action<PeerInformation> OnFoundPeer;
        public event System.Action<PeerInformation> OnLostPeer;

        string broadcastAddress;
        float currentTime;
        UdpClient send, recv;
        byte[] helloMsg, goodbyeMsg;
        float broadcastPeriod;
        string localHost = "";
        byte[] localIP;

        int appIdHash, groupIdHash;
    
        void Start ()
        {
            broadcastAddress = GetBroadcastIP ().ToString ();
            Application.runInBackground = true;
            localHost = Dns.GetHostName ();
            IPAddress[] addresses = null;
            try {
                addresses = Dns.GetHostEntry (localHost).AddressList;
            } catch (SocketException) {
                try {
                    addresses = Dns.GetHostEntry (localHost + ".local").AddressList;
                } catch (SocketException e) {
                    Debug.LogError (e.ToString ());
                }
            }
            if (addresses != null) {
                foreach (var ip in addresses) {
                    if (ip.AddressFamily == AddressFamily.InterNetwork) {
                        localIP = ip.GetAddressBytes();
                    }
                }
            }
            appIdHash = HashString(appId);
            groupIdHash = HashString(groupId);
            send = new UdpClient ();
            recv = new UdpClient (discoveryPort);
            send.DontFragment = true;
            send.EnableBroadcast = true;
            ConstructBroadcastMessages ();
            broadcastPeriod = 1f / broadcastFrequency;
        }

        void ConstructBroadcastMessages ()
        {
            using (var ms = new System.IO.MemoryStream ()) {
                var writer = new System.IO.BinaryWriter (ms);
                writer.Write (appIdHash);
                writer.Write (groupIdHash);
                writer.Write (HELLO);
                writer.Write (localHost);
                writer.Write (localIP);
                helloMsg = ms.ToArray ();
            }
            using (var ms = new System.IO.MemoryStream ()) {
                var writer = new System.IO.BinaryWriter (ms);
                writer.Write (appIdHash);
                writer.Write (groupIdHash);
                writer.Write (GOODBYE);
                writer.Write (localHost);
                writer.Write (localIP);
                goodbyeMsg = ms.ToArray ();
            }
        }

        public void SetGroupID(string id) {
            if(id != groupId) {
                send.Send (goodbyeMsg, goodbyeMsg.Length, broadcastAddress, discoveryPort);
                groupId = id;
                groupIdHash = HashString(groupId);
                ConstructBroadcastMessages();
                send.Send (helloMsg, helloMsg.Length, broadcastAddress, discoveryPort);
            }
        }
        List<PeerInformation> deadPeers = new List<PeerInformation>();
        void Update ()
        {
            currentTime = Time.realtimeSinceStartup;
            broadcastPeriod -= Time.unscaledDeltaTime;
            if (broadcast && broadcastPeriod < 0) {
                SendBroadcast ();
                broadcastPeriod = 1f / broadcastFrequency;
            }
            deadPeers.Clear();
            for (int i = 0; i < peers.Count; i++) {
                var peer = peers [i];
                if (currentTime - peer.lastSeen > maxAge) {
                    if (OnLostPeer != null)
                        OnLostPeer (peer);
                    deadPeers.Add(peer);
                }
            }
            for (int i = 0; i < deadPeers.Count; i++) {
                var p = deadPeers [i];
                peers.Remove (p);
            }
            ReceiveBroadcast ();
        }

        void SendBroadcast ()
        {
            if(helloMsg == null || send == null) return;
            try {
                send.Send (helloMsg, helloMsg.Length, broadcastAddress, discoveryPort);
            } catch (SocketException) {

            }
        }

        void ReceiveBroadcast ()
        {
            if (recv.Available == 0)
                return;
            var endPoint = new IPEndPoint (IPAddress.Any, discoveryPort);
            using (var ms = new System.IO.MemoryStream(recv.Receive (ref endPoint))) {
                var reader = new System.IO.BinaryReader (ms);

                if (reader.ReadInt32 () != appIdHash)
                    return;
                if (reader.ReadInt32 () != groupIdHash)
                    return;
                var greeting = reader.ReadInt32 ();

                var otherHost = reader.ReadString ();
                if (otherHost != localHost) {
                    PeerInformation peer = null;
                    foreach (var p in peers) {
                        if (p.hostname == otherHost) {
                            peer = p;
                        }
                    }
                    if (peer == null) {
                        peer = new PeerInformation ();
                        peers.Add (peer);
                        peer.hostname = otherHost;
                        peer.ipAddress = new IPAddress(reader.ReadBytes(4));
                        if (OnFoundPeer != null)
                            OnFoundPeer (peer);
                    }
                    if (greeting == GOODBYE) {
                        if (OnLostPeer != null)
                            OnLostPeer (peer);
                        peers.Remove (peer);
                    } else {
                        peer.lastSeen = currentTime;
                    }
                }

            }

        }

        void StopDiscovery ()
        {
            send.Send (goodbyeMsg, goodbyeMsg.Length, broadcastAddress, discoveryPort);
            recv.Close ();
        }
    
        void OnApplicationQuit ()
        {
            StopDiscovery ();
        }
    
        void OnDestroy ()
        {
            StopDiscovery ();
        }

        IPAddress GetBroadcastIP ()
        {
            var maskIP = IPAddress.Parse (networkMask);
            var hostIP = IPAddress.Parse (Network.player.ipAddress);
        
            if (maskIP == null || hostIP == null)
                return null;
        
            byte[] complementedMaskBytes = new byte[4];
            byte[] broadcastIPBytes = new byte[4];
        
            for (int i = 0; i < 4; i++) {
                complementedMaskBytes [i] = (byte)~ (maskIP.GetAddressBytes ().ElementAt (i));
                broadcastIPBytes [i] = (byte)((hostIP.GetAddressBytes ().ElementAt (i)) | complementedMaskBytes [i]);
            }
            return new IPAddress (broadcastIPBytes);
        }

        static public int HashString (string text)
        {
            unchecked {
                var hash = 23;
                for (int i = 0; i < text.Length; i++) {
                    hash = hash * 31 + text [i];
                }
                return hash;
            }
        }

        static int HELLO = 1;
        static int GOODBYE = 2;

    
    }
}
