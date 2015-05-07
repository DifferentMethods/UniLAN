using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading;

namespace UniLAN
{
    /// <summary>
    /// A component which connects to all other NetworkPeer components on applications over the LAN.
    /// </summary>
    [RequireComponent(typeof(NetworkFinder))]
    public class NetworkPeer : MonoBehaviour
    {

        public int port = 1984;
        public bool debug = true;
        public string friendlyName = "";

        public event System.Action<NetworkMessage> OnMessageReceived;
        public event System.Action<int, string> OnNewPeer;

        public void RegisterNetworkedGameObject (int networkID, NetworkGameObject networkedGameObject)
        {
            nobjects.Add (networkID, networkedGameObject);
        }

        public void ConnectTo (IPAddress ipAddress)
        {
            lock (pendingConnections) {
                pendingConnections.Add (ipAddress);
            }
        }

        public void SetFriendlyName(string name) {
            friendlyName = name;
            SendGreeting();
        }

        public void SendGreeting(PeerSocket peer=null) {
            var greeting = NetworkMessage.Take();
            greeting.FromUID = this.UID;
            greeting.MessageType = MessageType.Greeting;
            greeting.Write (friendlyName);
            if(peer==null) {
                Send (greeting);
                NetworkMessage.Recycle(greeting);
            } else {
                peer.Send(greeting);
            }

        }

        public void SetGroup (string groupId)
        {
            finder.SetGroupID (groupId);
            lock (sockets) {
                foreach (var s in sockets.Values.ToArray()) {
                    s.Close ();
                }
                sockets.Clear ();
            }
        }

        /// <summary>
        /// The unique identifier of this peer.
        /// </summary>
        /// <value>The user interface.</value>
        public int UID;
            

        /// <summary>
        /// The number of connected peers.
        /// </summary>
        /// <value>The peer count.</value>
        public int PeerCount {
            get {
                return sockets.Count;
            }
        }

        public void Update ()
        {
            ExecuteScheduledCalls ();
            if (inbox.Count > 0) {
                lock (inbox) {
                    for (int i = 0; i < inbox.Count; i++) {
                        var m = inbox [i];
                        switch (m.MessageType) {
                        case MessageType.RemoteCall:
                            var networkId = m.ReadInt ();
                            if (nobjects.ContainsKey (networkId)) 
                                nobjects [networkId].InvokeRPC (m);
                            else
                                Debug.LogError ("Unknown networkId received.");
                            break;
                        case MessageType.Greeting:
                            if(m.FromUID == this.UID) {
                                UID = Random.Range(int.MinValue, int.MaxValue);
                                SendGreeting();
                            }
                            if(OnNewPeer != null)
                                OnNewPeer(m.FromUID, m.ReadString());
                            break;
                        default:
                            if (OnMessageReceived != null)
                                OnMessageReceived (m);
                            break;
                        }
                        NetworkMessage.Recycle (m);
                    }
                    inbox.Clear ();
                }
            }
        }

        /// <summary>
        /// Send a message to all peers.
        /// </summary>
        /// <param name="msg">Message.</param>
        public void Send (NetworkMessage msg)
        {
            if (PeerCount > 0) {
                msg.FromUID = this.UID;
                msg.ToUID = 0;
                lock (outbox) {
                    outbox.Add (msg.Copy ());
                }
            }
        }

        /// <summary>
        /// Send a message to all peers.
        /// </summary>
        /// <param name="msg">Message.</param>
        public void SendTo (int ToUID, NetworkMessage msg)
        {
            if (PeerCount > 0) {
                msg.FromUID = this.UID;
                msg.ToUID = ToUID;
                lock (outbox) {
                    outbox.Add (msg.Copy ());
                }
            }
        }

        void PerformNetworkingOperations ()
        {
            try {
                try {

                    while (true) {
                        AcceptConnections ();
                        MakeConnections ();
                        SendAndReceive ();
                        Thread.Sleep (0);
                    }
                     
                } finally {
                    main.Close ();
                    lock (sockets) {
                        foreach (var s in sockets.Values) {
                            s.Close ();
                        }
                    }
                }
            } catch (ThreadAbortException) {
                Log ("Closing Handler Thread.");
            } catch (System.Exception e) {
                Log ("Error in Socket Handler Thread: " + e.ToString ());
            }

        }

        List<IPAddress> ucp = new List<IPAddress> ();

        void MakeConnections ()
        {
            if (pendingConnections.Count == 0)
                return;
            ucp.Clear ();
            lock (pendingConnections) {
                ucp.AddRange (pendingConnections);
                pendingConnections.Clear ();
            }

            foreach (var addr in ucp.ToArray()) {
                if (IsConnectedTo (addr)) {
                    Log ("Already connected to: " + addr);
                    ucp.Remove (addr);
                    continue;
                }
                Log ("Connecting to: " + addr);
                var socket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.SetSocketOption (SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
                socket.Blocking = false;
                try {
                    socket.Connect (addr, port);
                } catch (SocketException e) {
                    if (e.SocketErrorCode == SocketError.WouldBlock) {
                        //This is OK.
                    } else {
                        Log ("Could not connect: " + e.SocketErrorCode.ToString ());
                        return;
                    }
                }
                ucp.Remove (addr);
                lock (sockets) {
                    if(!IsConnectedTo(addr)) {
                        var peer = sockets [socket] = new PeerSocket (socket);
                        Log ("Connected.");
                        SendGreeting(peer);
                    } else {
                        Log ("Already connected to: " + addr);
                        socket.Close();
                    }

                }
            }
            if (ucp.Count > 0) {
                lock (pendingConnections) {
                    pendingConnections.AddRange (ucp);
                }
            }
            
        }

        void AcceptConnections ()
        {

            if (main == null) {
                Log ("Creating new listener socket.");
                main = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                main.Blocking = false;
                var iep = new IPEndPoint (IPAddress.Any, port);
                main.Bind (iep);
                main.Listen (10);
            } 

            try {
                var ready = main.Poll (0, SelectMode.SelectRead);
                if (ready) {    
                    Log ("Accepting connection");
                    var socket = main.Accept ();
                    socket.Blocking = false;
                    socket.SetSocketOption (SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
                    var rip = socket.RemoteEndPoint as IPEndPoint;
                    Log ("Accepted connection from: " + rip.Address.ToString ());
                    lock(sockets) {
                        if (IsConnectedTo (rip.Address)) {
                            Log ("Already connected to: " + rip.Address.ToString () + ", closing.");
                            socket.Close ();
                        } else {
                            var peer = sockets [socket] = new PeerSocket (socket);
                            SendGreeting(peer);
                        }
                    }
                }
            } catch (SocketException) {
                Log ("Socket error on listener");
                main.Close ();
                main = null;
            }

        }

        bool ReadFromSocket (Socket socket)
        {
            PeerSocket buffer;
            lock (sockets) {
                buffer = sockets [socket];
            }
            try {
                var finished = buffer.Read ();
                var msgs = buffer.ReadIncoming ();
                if (msgs.Length > 0) {
                    lock (inbox) {
                        inbox.AddRange (msgs);
                    }
                }
                return finished;
            } catch (SocketException e) {
                Log ("Read error on socket, closing." + e.SocketErrorCode.ToString ());
                socket.Close ();
                return true;
            }
        }

        bool WriteToSocket (Socket socket)
        {
            PeerSocket buffer;
            lock (sockets) {
                buffer = sockets [socket];
            }
            try {
                buffer.Write ();
            } catch (SocketException e) {
                Log ("Write error on socket, closing." + e.SocketErrorCode.ToString ());
                socket.Close ();
                return true;
            }
            return false;
        }

        void SendMessagesToOutboxQueue ()
        {
            lock (outbox) {
                if (outbox.Count > 0) {
                    lock (sockets) {
                        foreach (var s in sockets.Values) {
                            s.Send ((
                                from i in outbox where (i.ToUID == 0 || i.ToUID == s.RemoteUID) select i.Copy ()));
                        }
                    }
                    for (int i = 0; i < outbox.Count; i++) {
                        var m = outbox [i];
                        NetworkMessage.Recycle (m);
                    }
                    outbox.Clear ();
                }
            }
        }

        void SendAndReceive ()
        {
            if (sockets.Count == 0)
                return;
            
            SendMessagesToOutboxQueue ();
            deadSockets.Clear ();
            lock (sockets) {
                foreach (var socket in sockets.Keys) {
                    var readable = false;
                    try {
                        readable = socket.Poll (0, SelectMode.SelectRead);
                    } catch (System.ObjectDisposedException) {
                        continue;
                    }
                    if (readable) {
                        var finished = ReadFromSocket (socket);
                        if (finished) {
                            Log ("Socket was closed.");
                            deadSockets.Add (socket);
                            continue;
                        } 
                    }
                    var writable = false;
                    try {
                        writable = socket.Poll (0, SelectMode.SelectWrite);
                    } catch (System.ObjectDisposedException) {
                        continue;
                    }
                    if (writable) {
                        var finished = WriteToSocket (socket);
                        if (finished)
                            deadSockets.Add (socket);
                    }
                }
                for (int i = 0; i < deadSockets.Count; i++) {
                    var s = deadSockets [i];
                    sockets.Remove (s);
                }
            }

        }

        bool IsConnectedTo (IPAddress addr)
        {
            var ips = addr.ToString();
            lock (sockets) {
                foreach (var k in sockets.Keys) {
                    var ipe = (IPEndPoint)k.RemoteEndPoint;
                    if (ipe == null)
                        continue;
                    if (ipe.Address.ToString() == ips)
                        return true;
                }
            }
            return false;
        }

        void Awake ()
        {
            UID = Random.Range(int.MinValue, int.MaxValue);
            finder = GetComponent<NetworkFinder> ();
        }

        void Start ()
        {
            Application.runInBackground = true;
            handler = new Thread (PerformNetworkingOperations);
            handler.Start ();
            finder.OnFoundPeer += HandleOnFoundPeer;
            finder.OnLostPeer += HandleOnLostPeer;
        }

        void HandleOnLostPeer (PeerInformation peer)
        {
            Log ("Lost Peer: " + peer.hostname);
            lock (pendingConnections) {
                if (pendingConnections.Contains (peer.ipAddress)) {
                    pendingConnections.Remove (peer.ipAddress);
                }
            }

        }

        void HandleOnFoundPeer (PeerInformation peer)
        {
            Log ("Found Peer: " + peer.hostname);
            lock (pendingConnections) {
                pendingConnections.Add (peer.ipAddress);
            }
        }

        void OnApplicationQuit ()
        {
            handler.Abort ();

            Log ("Handler shut down.");
        }

        void Log (object message)
        {
            if (debug)
                Debug.Log ("UniLAN: " + message.ToString ());
        }

        void ScheduleCall (System.Action fn)
        {
            lock (mainLoopSchedule) {
                mainLoopSchedule.Add (fn);
            }
        }

        void ExecuteScheduledCalls ()
        {
            lock (mainLoopSchedule) {
                foreach (var i in mainLoopSchedule) {
                    i ();
                }
                mainLoopSchedule.Clear ();
            }
        }

        List<IPAddress> pendingConnections = new List<IPAddress> ();
        NetworkFinder finder;
        Thread handler;
        Dictionary<Socket, PeerSocket> sockets = new Dictionary<Socket, PeerSocket> ();
        List<NetworkMessage> inbox = new List<NetworkMessage> ();
        List<NetworkMessage> outbox = new List<NetworkMessage> ();
        List<Socket> deadSockets = new List<Socket> ();
        Socket main;
        Dictionary<int, NetworkGameObject> nobjects = new Dictionary<int, NetworkGameObject> ();
        List<System.Action> mainLoopSchedule = new List<System.Action> ();

    }
}
