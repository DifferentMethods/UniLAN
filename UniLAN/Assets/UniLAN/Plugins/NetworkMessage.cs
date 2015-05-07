using UnityEngine;
using System.Collections;
using UniExtensions.Collections;
using UniExtensions.Resource;
using System;
using System.Runtime.InteropServices;
using System.IO;

namespace UniLAN
{
    public class NetworkMessage
    {
        #region CLASS_API
        static ObjectPool<NetworkMessage> messagePool = new ObjectPool<NetworkMessage> (256);
        public const int MAX_SIZE = 4096;
       
        public static int MessagesInUse {
            get;
            private set;
        }

        public static NetworkMessage Take ()
        {
            lock (messagePool) {
                MessagesInUse++;
                var msg = messagePool.Take ();
                msg.locked = false;
                msg.Clear ();
                return msg;
            }
        }

        public static void Recycle (NetworkMessage msg)
        {
            lock (messagePool) {
                MessagesInUse--;
                msg.locked = true;
                messagePool.Recycle (msg);
                msg = null;
            }
        }

        #endregion

        #region INSTANCE_API

        public byte[] buffer = new byte[MAX_SIZE];
        BinaryReader reader;
        BinaryWriter writer;
        MemoryStream writeStream;
        MemoryStream readStream;

        public NetworkMessage ()
        {
            this.locked = true;
            writeStream = new MemoryStream(buffer);
            readStream = new MemoryStream(buffer);
            writer = new BinaryWriter(writeStream);
            reader = new BinaryReader(readStream);
            readStream.Position = 12;
            writeStream.Position = 12;
        }

        public int Length {
            get {
                return (int)writer.BaseStream.Position;
            }
        }

        int _UID;
        public int FromUID {
            get {
                return GetIntAt(0);
            }
            set {
                SetIntAt(value, 0);
            }
        }

        public MessageType MessageType {
            get {
                return (MessageType)GetIntAt(8);
            }
            set {
                SetIntAt((int)value, 8);
            }
        }

        public int ToUID {
            get {
                return GetIntAt(4);
            }
            set {
                SetIntAt(value, 4);
            }
        }

        public void Clear ()
        {
            readStream.Position = 12;
            writeStream.Position = 12;
            FromUID = 0;
            ToUID = 0;
        }

        public NetworkMessage Copy ()
        {
            CheckLock ();
            var m = Take ();
            m.CheckLock ();
            this.buffer.CopyTo (m.buffer, 0);
            m.readStream.Position = readStream.Position;
            m.writeStream.Position = writeStream.Position;
            return m;
        }

        public void CheckLock ()
        {
            if (this.locked) {
                Debug.Log(MessageType);
                Debug.Log(System.Text.ASCIIEncoding.ASCII.GetString(buffer));
                throw new System.ObjectDisposedException ("Message has been recycled!");
            }
        }

        #region PRIMITIVE_READERS
        public int ReadInt ()
        {
            return reader.ReadInt32();
        }
        
        public int ReadEnum ()
        {
            return reader.ReadInt32();
        }
        
        public byte ReadByte ()
        {
            return reader.ReadByte();
        }
        
        public double ReadDouble ()
        {
            Buffer.BlockCopy(buffer, (int)readStream.Position, DOUBLE, 0, 8);
            readStream.Position += 8;
            return DOUBLE[0];
        }
        
        public long ReadLong ()
        {
            return reader.ReadInt64();
        }
        
        public float ReadFloat ()
        {
            Buffer.BlockCopy(buffer, (int)readStream.Position, FLOAT, 0, 4);
            readStream.Position += 4;
            return FLOAT[0];
        }
        
        public bool ReadBool ()
        {
            return reader.ReadBoolean();
        }
        
        public string ReadString ()
        {
            return reader.ReadString();
        }
        
        public char ReadChar ()
        {
            return reader.ReadChar();
        }
        #endregion
        #region EXTENDED_READERS
        public Color ReadColor ()
        {
            return new Color (ReadFloat (), ReadFloat (), ReadFloat (), ReadFloat ());
        }
        
        public Vector2 ReadVector2 ()
        {
            return new Vector2 (ReadFloat (), ReadFloat ());
        }
        
        public Vector3 ReadVector3 ()
        {
            return new Vector3 (ReadFloat (), ReadFloat (), ReadFloat ());
        }
        
        public Vector4 ReadVector4 ()
        {
            return new Vector4 (ReadFloat (), ReadFloat (), ReadFloat (), ReadFloat ());
        }
        
        public Quaternion ReadQuaternion ()
        {
            return new Quaternion (ReadFloat (), ReadFloat (), ReadFloat (), ReadFloat ());
        }
        
        public object ReadObject ()
        {
            var json = ReadString ();
            json.GetHashCode ();
            return UniExtensions.Serialization.JsonSerializer.Decode (json);
        }
        
        public T ReadObject<T> () where T : class, new()
        {
            var json = ReadString ();
            return UniExtensions.Serialization.JsonSerializer.Decode<T> (json);
        }
        #endregion
        #region PRIMITIVE_WRITERS
        void Write (byte[] bytes)
        {
            writer.Write(bytes, 0, bytes.Length);
        }

        public void Write (byte value)
        {
            writer.Write(value);
        }

        public void Write (System.Enum value)
        {
            writer.Write(Convert.ToInt32(value));
        }
        
        public void Write (int value)
        {
            writer.Write(value);
        }

        double[] DOUBLE = new double[1];
        public void Write (double value)
        {
            DOUBLE[0] = value;
            Buffer.BlockCopy(DOUBLE, 0, buffer, (int)writeStream.Position, 8);
            writeStream.Position += 8;
        }
        
        public void Write (long value)
        {
            writer.Write(value);
        }

        float[] FLOAT = new float[1];
        public void Write (float value)
        {
            FLOAT[0] = value;
            Buffer.BlockCopy(FLOAT, 0, buffer, (int)writeStream.Position, 4);
            writeStream.Position += 4;
        }
        
        public void Write (bool value)
        {
            writer.Write(value);
        }
        
        public void Write (string value)
        {
            writer.Write(value);
        }
        
        public void Write (char value)
        {
            writer.Write(value);
        }
        #endregion
        #region EXTENDED_WRITERS
        public void Write (Color value)
        {
            Write (value.r);
            Write (value.g);
            Write (value.b);
            Write (value.a);
        }

        public void Write (Vector2 value)
        {
            Write (value.x);
            Write (value.y);
        }

        public void Write (Vector3 value)
        {
            Write (value.x);
            Write (value.y);
            Write (value.z);
        }

        public void Write (Vector4 value)
        {
            Write (value.x);
            Write (value.y);
            Write (value.z);
            Write (value.w);
        }

        public void Write (Quaternion value)
        {
            Write (value.x);
            Write (value.y);
            Write (value.z);
            Write (value.w);
        }

        public void WriteObject (object obj)
        {
            Write (UniExtensions.Serialization.JsonSerializer.Encode (obj));
        }

        public void Write (object obj)
        {
            if (obj is Boolean) 
                Write ((bool)obj);
            else if (obj is Byte)
                Write ((byte)obj);
            else if (obj is Double)
                Write ((double)obj);
            else if (obj is Int32)
                Write ((int)obj);
            else if (obj is Single)
                Write ((float)obj);
            else if (obj is String)
                Write ((string)obj);
            else if (obj is Vector2)
                Write ((Vector2)obj);
            else if (obj is Vector3)
                Write ((Vector3)obj);
            else if (obj is Vector4)
                Write ((Vector4)obj);
            else if (obj is Quaternion)
                Write ((Quaternion)obj);
            else if (obj is Color)
                Write ((Color)obj);
        }
        #endregion
        #endregion

        #region IMPLEMENTATION 
        int GetIntAt(int index) {
            var p = readStream.Position;
            readStream.Seek(index, SeekOrigin.Begin);
            var i = reader.ReadInt32();
            readStream.Position = p;
            return i;
        }
        void SetIntAt(int value, int index) {
            var p = writeStream.Position;
            writeStream.Seek(index, SeekOrigin.Begin);
            writer.Write(value);
            writeStream.Position = p;
        }
        #endregion

        bool locked = true;




    }
}
