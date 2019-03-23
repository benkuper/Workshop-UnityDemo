using System;
using UnityEngine;
using System.Collections.Generic;
using UnityOSC;


public class OSCMaster : MonoBehaviour
{
    public static OSCMaster instance;

    OSCServer server;

    [Header("Connection")]
    public int localPort;
    public bool isConnected;

    [Header("Log")]
    public bool logIncoming;
    public bool logOutgoing;

    [Header("Routing")]
    public bool useOCFPrefix;

    OSCClient client;
   
    Controllable[] controllables;

    //public delegate void ValueUpdateReadyEvent(string target, string property, List<object> objects);
    //public event ValueUpdateReadyEvent valueUpdateReady;

    public delegate void MessageAvailable(OSCMessage message);
    public event MessageAvailable messageAvailable;

    // Use this for initialization
    void Awake()
    {
        instance = this;
        client = new OSCClient(System.Net.IPAddress.Loopback, 0, false);
    }

    public void Connect()
    {
        Debug.Log("[OCF] Connecting to port " + localPort);
        try
        {
            if(server != null)
                server.Close();

            server = new OSCServer(localPort);
            server.PacketReceivedEvent += packetReceived;
        
            server.Connect();
            isConnected = true;
        }
        catch (Exception e)
        {
            Debug.LogError("Error with port " + localPort);
            Debug.LogWarning(e.StackTrace);
            isConnected = false;
            server = null;
        }
    }

    void packetReceived(OSCPacket p)
    {

        if (p.IsBundle())
        {
            foreach (OSCMessage m in p.Data)
            {
                processMessage(m);
            }
        }else processMessage((OSCMessage)p);
       // Debug.Log("Packet processed");
    }

    void processMessage(OSCMessage m)
    {
        if (logIncoming) Debug.Log("Message received : "+m.Address+" ("+m.Data.Count+" arguments)");

        string[] addressSplit = m.Address.Split(new char[] { '/' });

        //First addressSplit is null because of /OCF/...
        if (useOCFPrefix && addressSplit[1] != "OCF" || addressSplit.Length < 3) //.Length != 3)
        {
			if (messageAvailable != null)
                messageAvailable(m); //propagate the message
             //if (logIncoming) Debug.LogWarning("Message " + m.Address + " is not a valid control address.");
            //return;
        }
        else if(addressSplit.Length >= 3)//Starts with /OCF/ so it's control
        {
			string target = "";
			string property = "";
			try {
				target = addressSplit[useOCFPrefix?2:1];
				property = addressSplit[useOCFPrefix?3:2];
			}
			catch(Exception e) {
				Debug.LogWarning("Error parsing OCF command : "+e.Message);
			}

			if (logIncoming) Debug.Log("> Routing to target : " + target + ", property = " + property);
            ControllableMaster.UpdateValue(target, property, m.Data);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(isConnected)
            server.Update();
    }

    public static void sendMessage(OSCMessage m, string host, int port)
    {
        if (instance.logOutgoing)
        {
            string args = "";
            for (int i = 0; i < m.Data.Count; i++) args += (i > 0 ? ", " : "") + m.Data[i].ToString();
            Debug.Log("Sending " + m.Address + " : "+args +" to "+host+":"+port);
        }

        instance.client.SendTo(m, host, port);
    }


    void OnApplicationQuit()
    {
        if (server != null)
            server.Close();
    }
}
