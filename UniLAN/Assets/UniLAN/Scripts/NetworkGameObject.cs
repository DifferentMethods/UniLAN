using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UniLAN
{
    public class NetworkGameObject : MonoBehaviour
    {


        public NetworkPeer peer;
        [HideInInspector]
        public int networkID;
        Dictionary<int, MethodReference> methods = new Dictionary<int, MethodReference> ();

        void Start ()
        {
            RegisterRPCMethods ();
            if (peer != null) {
                peer.RegisterNetworkedGameObject (networkID, this);
                peer.OnNewPeer += OnNewConnection;
                peer.OnMessageReceived += OnMessageReceived;
            }
        }

        protected virtual void OnMessageReceived (NetworkMessage msg)
        {
        }

        protected virtual void OnNewConnection(int UID, string friendlyName) {
        }

        void Reset ()
        {
            int c;
            do {
                c = Random.Range (int.MinValue, int.MaxValue);
            } while(!GUIDAvailable(c));
            networkID = c;
            var np = GameObject.FindObjectsOfType<NetworkPeer> ();
            if (np.Length == 1) {
                peer = np [0];
            }
        }

        bool GUIDAvailable (int c)
        {
            foreach (var o in GameObject.FindObjectsOfType<NetworkGameObject>()) {
                if (o.networkID == c) {
                    return false;
                }
            }
            return true;
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

        protected void SendRPC(string fn, params object[] parameters) {
            var key = GetType().Name + "." + fn;
            _SendRPC(HashString(key), parameters);
        }

        protected void SendRPC(int rpcId, params object[] parameters) {
            _SendRPC(rpcId, parameters);
        }

        protected int GetRpcId(string fn) {
            var key = GetType().Name + "." + fn;
            return HashString(key);
        }

        void _SendRPC (int id, object[] parameters)
        {
            var msg = NetworkMessage.Take ();
            msg.MessageType = MessageType.RemoteCall;
            msg.Write (networkID);
            msg.Write (id);
            msg.Write (parameters.Length);
            foreach (var p in parameters) {
                msg.WriteTypedValue (p);
            }
            peer.Send (msg);
            NetworkMessage.Recycle (msg);
        }

        public void InvokeRPC (NetworkMessage m)
        {
            var rpcId = m.ReadInt ();
            var argCount = m.ReadInt ();
            var args = new object[argCount];
            for (var i = 0; i<argCount; i++) {
                args [i] = m.ReadTypedValue();
            }
            methods [rpcId].Invoke (args);
        }

        void RegisterRPCMethods ()
        {
            foreach (var c in gameObject.GetComponents<Component>()) {
                foreach (var mi in c.GetType().GetMethods()) {
                    if (mi.GetCustomAttributes (typeof(RPCAttribute), true).Length > 0) {
                        var name = c.GetType ().Name + "." + mi.Name;
                        var key = HashString (name);
                        Debug.Log (string.Format ("Exposing RPC method {0} with key {1}.", name, key));
                        methods.Add (key, new MethodReference (c, mi));
                    }
                }
            }
        }

    }
}