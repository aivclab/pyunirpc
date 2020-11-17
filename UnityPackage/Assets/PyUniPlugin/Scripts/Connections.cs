using System;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;
using UnityEngine;

namespace PyUniPlugin
{
    public class ZmqClient
    {
        private RequestSocket client;
        
        public ZmqClient(int port)
        {
            client = new RequestSocket();
            client.Connect("tcp://localhost:" + port);
            Debug.Log($"Started client at {port}");
        }

        public ZmqClient(int port, string host)
        {
            client = new RequestSocket();
            client.Connect($"tcp://{host}:" + port);
            Debug.Log($"Started client at {client.ToString()}");
        }

        public PyUniType SendData(string data)
        {
            return SendData(data, TimeSpan.FromSeconds(2.0));
        }

        public PyUniType SendData(PyUniCall data)
        {
            var str = data.PackJSON();

            return SendData(str, TimeSpan.FromSeconds(2.0));
        }
        
        public PyUniType SendData(PyUniCall data, TimeSpan timeout)
        {
            var str = data.PackJSON();

            return SendData(str, timeout);
        }

        public PyUniType SendData(string data, TimeSpan timeout)
        {
            client.SendFrame(data);
            client.TryReceiveFrameString(timeout, out string message);

            return PyUniType.JsonConstructor<PyUniResult>(message);
        }
    }
}
