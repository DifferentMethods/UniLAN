using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniExtensions.Collections;
using System.Net.Sockets;

namespace UniLAN
{
    public class PeerSocket
    {
        public int RemoteUID {
            get;
            private set;
        }

        public bool Closed {
            get;
            private set;
        }

        List<NetworkMessage> outgoing, incoming;
        Socket socket;
        IEnumerator reader, writer;

        public PeerSocket (Socket socket)
        {
            this.socket = socket;
            outgoing = new List<NetworkMessage> (8);
            incoming = new List<NetworkMessage> (8);
            reader = Reader ();
            writer = Writer ();
            Closed = false;
        }

        public void Send(NetworkMessage m) {
            outgoing.Add(m);
        }

        public void Send (IEnumerable<NetworkMessage> networkMessages)
        {
            outgoing.AddRange (networkMessages);
        }

        public NetworkMessage[] ReadIncoming ()
        {
            var msgs = incoming.ToArray ();
            incoming.Clear ();
            return msgs;
        }

        public void Close ()
        {
            Closed = true;
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
            reader = writer = null;
            foreach(var i in incoming) {
                NetworkMessage.Recycle(i);
            }
            foreach(var i in outgoing) {
                NetworkMessage.Recycle(i);
            }
        }

        IEnumerator Writer ()
        {
            var fourBytes = new byte[4];
            while (true) {
                while (outgoing.Count > 0) {
                    var msg = outgoing [0];
                    outgoing.RemoveAt (0);
                    if (msg == null)
                        throw new System.NullReferenceException ("ARG");
                    var size = msg.Length;
                    fourBytes = System.BitConverter.GetBytes (size);
                    var count = 0;
                    while (count < 4) {
                        var sent = 0;
                        try {
                            sent = socket.Send (fourBytes, count, 4 - count, SocketFlags.None);
                        } catch (SocketException e) {
                            if (e.SocketErrorCode == SocketError.WouldBlock) {
                                //This is ok
                            } else {
                                throw;
                            }
                        }
                        count += sent;
                        if (count < 4) {
                            yield return null;
                        }
                    }
                    count = 0;
                    while (count < size) {
                        var sent = 0;
                        try {
                            sent = socket.Send (msg.buffer, count, size - count, SocketFlags.None);
                        } catch (SocketException e) {
                            if (e.SocketErrorCode == SocketError.WouldBlock) {
                                //This is ok
                            } else {
                                throw;
                            }
                        }
                        count += sent;
                        if (count < size)
                            yield return null;
                    }
                    NetworkMessage.Recycle (msg);
                }
                yield return null;
            }

        }

        IEnumerator Reader ()
        {
            var buffer = new byte[NetworkMessage.MAX_SIZE];
            while (true) {
                var count = 0;
                while (count < 4) {
                    var recv = socket.Receive (buffer, count, 4 - count, SocketFlags.None);
                    if (recv == 0) {
                        yield return true;
                        yield break;
                    }
                    count += recv;
                    if (count < 4)
                        yield return false;
                }

                var size = System.BitConverter.ToInt32 (buffer, 0);
                if (size > NetworkMessage.MAX_SIZE) {
                    Debug.LogWarning ("Message size too large: " + size);
                    socket.Close ();
                    yield return true;
                }
                if (socket.Available == 0)
                    yield return false;
                count = 0;
                while (count < size) {
                    var recv = socket.Receive (buffer, count, size - count, SocketFlags.None);
                    if (recv == 0) {
                        yield return true;
                        yield break;
                    }
                    count += recv;
                    if (count < size)
                        yield return false;
                }
                var msg = NetworkMessage.Take ();
                buffer.CopyTo (msg.buffer, 0);
                this.RemoteUID = msg.FromUID;
                incoming.Add (msg);
                yield return false;
            }
        }

        public bool Read ()
        {
            reader.MoveNext ();
            return (bool)reader.Current;
        }

        public void Write ()
        {
            writer.MoveNext ();
        }
    }
}
