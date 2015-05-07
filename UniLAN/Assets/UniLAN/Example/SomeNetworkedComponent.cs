using UnityEngine;
using System.Collections;

public class SomeNetworkedComponent : UniLAN.NetworkGameObject {

    public float xyz;
	public Vector3 abc;

    [UniLAN.RPC]
    public void SetXYZ(float v) {
        this.xyz = v;
    }

    [UniLAN.RPC]
    public void SetABC(Vector3 v, float x) {
        this.abc = v * x;
    }

    protected override void OnNewConnection (int UID, string friendlyName)
    {
        Debug.Log(friendlyName + " has arrived. (" + UID + ")");
    }

    void Update() {
        SendRPC("SetABC", Random.onUnitSphere, Random.Range(10,100));
    }

}
