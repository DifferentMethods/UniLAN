using UnityEngine;
using System.Collections;

namespace UniLAN
{
    static public class ParameterSerializer
    {
        static System.Type[] allowedParameterTypes = new System.Type[] {
            typeof(int),
            typeof(float),
            typeof(double),
            typeof(long),
            typeof(char),
            typeof(bool),
            typeof(string),
            typeof(Vector2),
            typeof(Vector3),
            typeof(Vector4),
            typeof(Quaternion),
            typeof(Color),
        };
        static System.Func<NetworkMessage, object>[] typeReaders = new System.Func<NetworkMessage, object>[] {
            (m) => m.ReadInt (),
            (m) => m.ReadFloat (),
            (m) => m.ReadDouble (),
            (m) => m.ReadLong (),
            (m) => m.ReadChar (),
            (m) => m.ReadBool (),
            (m) => m.ReadString (),
            (m) => m.ReadVector2 (),
            (m) => m.ReadVector3 (),
            (m) => m.ReadVector4 (),
            (m) => m.ReadQuaternion (),
            (m) => m.ReadColor (),
        };
        static System.Action<NetworkMessage, object>[] typeWriters = new System.Action<NetworkMessage, object>[] {
            (m,v) => m.Write ((int)v),
            (m,v) => m.Write ((float)v),
            (m,v) => m.Write ((double)v),
            (m,v) => m.Write ((long)v),
            (m,v) => m.Write ((char)v),
            (m,v) => m.Write ((bool)v),
            (m,v) => m.Write ((string)v),
            (m,v) => m.Write ((Vector2)v),
            (m,v) => m.Write ((Vector3)v),
            (m,v) => m.Write ((Vector4)v),
            (m,v) => m.Write ((Quaternion)v),
            (m,v) => m.Write ((Color)v),
        };

        static public void WriteTypedValue (this NetworkMessage m, object p)
        {
            var index = System.Array.IndexOf (allowedParameterTypes, p.GetType ());
            m.Write((byte)index);
            typeWriters [index] (m, p);
        }

        static public object ReadTypedValue (this NetworkMessage m)
        {
            var index = m.ReadByte ();
            return typeReaders [index] (m);
        }

    }
}