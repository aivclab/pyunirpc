using System;
using System.Collections.Generic;
using System.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PyUniPlugin.Scripts
{
    public class Test : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
            TestCall();
            TestResult();
            TestError();
            TestArray();
            TestConnection();
        }

        private void TestConnection()
        {
            var client = new ZmqClient(6789);
            var call = new PyUniCall("testfun", 69420, new List<object>(), new Dictionary<string, object>());
            Debug.Log(client.SendData(call));
            call.args = new List<object>(new object[]{5, 7});
            call.kwargs = new Dictionary<string, object>();
            call.kwargs.Add("array", new PyUniValNdArray(new float[] {5.5f,6e-3f,7.00007f,80.08f,9}));
            var res = client.SendData(call);
            Debug.Log(res);
            Debug.Log((res as PyUniResult).result[0]);
        }

        private void TestArray()
        {
            PyUniValNdArray arr = new PyUniValNdArray("float32", new []{3}, "AEYARwBI");
            var json = arr.PackJSON();
            print(json);
        }

        private void TestError()
        {
            string json =
                @"{'rpc_tag': '__RPC_ERROR__', 
                'handle': 'testfun', 
                'uid': 666, 
                'exception': 'ValueError',
                'descr' : 'whoops'
                }";
            
            var result = PyUniType.JsonConstructor<PyUniResult>(json);
            try
            {
                var res = result as PyUniResult;
                Debug.Log(res.result[0]);
            }
            catch (Exception)
            {
                var err = result as PyUniError;
                Debug.LogError($"{err.exception}: {err.descr}");
            }
            
        }

        private void TestResult()
        {
            string json =
                @"{'rpc_tag': '__RPC_RESULT__', 
                'handle': 'testfun', 
                'uid': 666, 
                'result': 
                    ['some string', 
                        {'rpc_tag': '__RPC_VAL_NDARRAY__', 
                        'dtype': 'int64', 
                        'shape': [3], 
                        'data': 'BgAAAAAAAAAHAAAAAAAAAAgAAAAAAAAA'}
                    ]
                }";

            PyUniResult call = PyUniType.JsonConstructor<PyUniResult>(json) as PyUniResult;

            string newjson = call.PackJSON();
            
            print(newjson);
        }

        private void TestCall()
        {
            string json =
                @"{'rpc_tag': '__RPC_CALL__', 
                'handle': 'testfun', 
                'uid': 666, 
                'args': ['lul', 'wut'],
                'kwargs':
                    {'some string': 
                        {'rpc_tag': '__RPC_VAL_NDARRAY__', 
                        'dtype': 'int64', 
                        'shape': [3], 
                        'data': 'BgAAAAAAAAAHAAAAAAAAAAgAAAAAAAAA'}
                    }
                }";

            PyUniCall call = PyUniType.JsonConstructor<PyUniCall>(json) as PyUniCall;

            string newjson = call.PackJSON();
            
            print(newjson);
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
