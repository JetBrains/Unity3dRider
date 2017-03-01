using UnityEditor;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace Plugins.Editor.JetBrains
{
  public class UdpCommandsHandler : MonoBehaviour {
    public delegate void RequestReceivedEventHandler(string message);
    public event RequestReceivedEventHandler OnRequestReceived;

    // Use this to trigger the event
    protected virtual void ThisRequestReceived(string message)
    {
      RequestReceivedEventHandler handler = OnRequestReceived;
      if(handler != null)
      {
        handler(message);
      }
    }

    // We use this to keep tasks needed to run in the main thread
    private static readonly Queue<Action> tasks = new Queue<Action>();

    public int requestPort = 11235;

    UdpClient udpRequestSender;
    UdpClient udpRequestReceiver;

    void Awake()
    {
      RiderPlugin.Log("Awake");
    }

    // Use this for initialization
    void Start () {
      RiderPlugin.Log("Start");

      // Set up the receiver for the requests
      // Listen for anyone looking for us
      udpRequestReceiver = new UdpClient();
      udpRequestReceiver.Client.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.ReuseAddress, true);
      var requestGroupEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), this.requestPort);
      udpRequestReceiver.Client.Bind(requestGroupEP);
      udpRequestReceiver.BeginReceive(AsyncRequestReceiveData, null);

      // Listen for the request
      this.OnRequestReceived += (message) => {
        RiderPlugin.Log("Request Received: " + message);
        // Do some more stuff when we get a request
        EditorApplication.ExecuteMenuItem(message);
      };
    }

    void Update () {
      HandleTasks();
    }

    void HandleTasks() {
      while (tasks.Count > 0)
      {
        Action task = null;

        lock (tasks)
        {
          if (tasks.Count > 0)
          {
            task = tasks.Dequeue();
          }
        }

        task();
      }
    }

    public void QueueOnMainThread(Action task)
    {
      lock (tasks)
      {
        tasks.Enqueue(task);
      }
    }

    void AsyncRequestReceiveData(IAsyncResult result)
    {
      IPEndPoint receiveIPGroup = new IPEndPoint(IPAddress.Any, this.requestPort);
      byte[] received;
      if (this.udpRequestReceiver != null) {
        received = this.udpRequestReceiver.EndReceive(result, ref receiveIPGroup);
      } else {
        return;
      }
      this.udpRequestReceiver.BeginReceive (new AsyncCallback(AsyncRequestReceiveData), null);
      string receivedString = System.Text.Encoding.ASCII.GetString(received);


      this.QueueOnMainThread(() => {
        // Fire the event
        this.ThisRequestReceived(receivedString);
      });

    }
  }
}