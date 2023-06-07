using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AustinHarris.JsonRpc;

public class test : MonoBehaviour
{
    class Rpc : JsonRpcService
    {
        [JsonRpcMethod]
        void Say(string message)
        {
            Debug.Log(message);
        }
    }

    Rpc rpc;
    // Start is called before the first frame update
    void Start()
    {
        rpc = new Rpc();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
