The test scene shows a simple setup for P2P LAN.

The NetworkFinder component is responsible for locating other peers
on the LAN.

The AppID uniquely identifies your app, and the group is used to 
identify groups within the app. If your network mask is set correctly
it should automagically find all other peers on your LAN.

The NetworkPeer component is used to send and receive messages on the
P2P network. If you inherit from NetworkGameObject, your component
will be able to send and receive RPC calls. See the 
SomeNetworkedComponent.cs file for a minimal example of this.

The NetworkPeer component can be used directly to send and receive
messages. It has an event which is fired when messages arrive:

void Start() {
   peer = GetComponent<NetworkPeer> ();
   peer.OnMessageReceived += HandleOnMessageReceived;
}

void HandleOnMessageReceived (NetworkMessage m)
{
    Debug.Log("Arrived: " + m.MessageType);
}

To send a message to the P2P network:

   var msg = NetworkMessage.Take ();
   msg.MessageType = MessageType.Event;
   msg.Write (yourVariables);
   peer.Send (msg);
   NetworkMessage.Recycle (msg);


