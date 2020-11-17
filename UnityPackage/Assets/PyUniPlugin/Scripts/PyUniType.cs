using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PyUniPlugin.Scripts;
using UnityEngine;

namespace PyUniPlugin
{
    [Serializable]
    public abstract class PyUniType
    {
        public RpcTag rpc_tag;
        public string handle;
        public int uid;

        public virtual string PackJSON()
        {
            string json = JsonConvert.SerializeObject(this);
            return json;
        }

        public static PyUniType JsonConstructor<T>(string jsonIn) where T : PyUniType
        {
            var res = JsonConvert.DeserializeObject<T>(jsonIn);
            switch (res.rpc_tag)
            {
                case RpcTag.__RPC_CALL__:
                    return JsonConvert.DeserializeObject<PyUniCall>(jsonIn);
                case RpcTag.__RPC_RESULT__:
                    var result = JsonConvert.DeserializeObject<PyUniResult>(jsonIn);
                    for (int i = 0; i < result.result.Count; i++)
                    {
                        try
                        {
                            result.result[i] = JsonConvert.DeserializeObject<PyUniValNdArray>(result.result[i].ToString());

                        }
                        catch (Exception e)
                        {
                            continue;
                        }
                    }

                    return result;
                case RpcTag.__RPC_ERROR__:
                    return JsonConvert.DeserializeObject<PyUniError>(jsonIn);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override string ToString()
        {
            return $"ID {uid} is {rpc_tag} for {handle}";
        }
    }

    [Serializable]
    public class PyUniCall : PyUniType
    {
        public List<object> args;
        public Dictionary<string, object> kwargs;

        public PyUniCall(string handle, int uid, List<object> args, Dictionary<string, object> kwargs)
        {
            this.rpc_tag = RpcTag.__RPC_CALL__;
            this.handle = handle;
            this.uid = uid;
            this.args = args;
            this.kwargs = kwargs;
        }
    }

    [Serializable]
    public class PyUniResult : PyUniType
    {
        public List<object> result;
        
        public PyUniResult(string handle, int uid, List<object> result)
        {
            this.rpc_tag = RpcTag.__RPC_RESULT__;
            this.handle = handle;
            this.uid = uid;
            this.result = result;
        }
    }

    [Serializable]
    public class PyUniError : PyUniType
    {
        public string exception;
        public string descr;
        
        public PyUniError(string handle, int uid, string exception, string descr)
        {
            this.rpc_tag = RpcTag.__RPC_ERROR__;
            this.handle = handle;
            this.uid = uid;
            this.exception = exception;
            this.descr = descr;
        }
    }

    [Serializable]
    public class PyUniValNdArray
    {
        public RpcTag rpc_tag;
        public string dtype;
        public int[] shape;
        public string data;

        public PyUniValNdArray(string dtype, int[] shape, string data)
        {
            this.rpc_tag = RpcTag.__RPC_VAL_NDARRAY__;
            this.dtype = dtype;
            this.shape = shape;
            this.data = data;
        }

        public PyUniValNdArray()
        {
        }

        public PyUniValNdArray(Array array, int[] shape = null)
        {
            this.rpc_tag = RpcTag.__RPC_VAL_NDARRAY__;
            this.dtype = CSharp2NumpyDict[array.GetType().GetElementType()];
            if (shape != null)
            {
                this.shape = shape;
            }
            else
            {
                this.shape = new int[array.Rank];
                for (int i = 0; i < array.Rank; i++)
                {
                    this.shape[i] = array.GetLength(i);
                }
            }

            int length = Buffer.ByteLength(array);
            byte[] buffer = new byte[length];
            Buffer.BlockCopy(array, 0, buffer, 0, length);
            this.data = Convert.ToBase64String(buffer);
        }

        private static Dictionary<Type, string> CSharp2NumpyDict = new Dictionary<Type, string>()
        {
            {typeof(sbyte),"int8"},
            {typeof(short),"int16"},
            {typeof(int),"int32"},
            {typeof(long),"int64"},
            {typeof(byte),"uint8"},
            {typeof(ushort),"uint16"},
            {typeof(uint),"uint32"},
            {typeof(ulong),"uint64"},
            {typeof(float),"float32"},
            {typeof(double),"float64"},
            {typeof(decimal),"longdouble"},
            {typeof(char),"ushort"},
        };

        private static Dictionary<string, Type> Numpy2CSharpDict =
            CSharp2NumpyDict.ToDictionary((i) => i.Value, (i) => i.Key);
        
        public Array GetData()
        {
            Array arr = Array.CreateInstance(Numpy2CSharpDict[dtype], shape);
            byte[] buffer = Convert.FromBase64String(data);
            Buffer.BlockCopy(buffer, 0, arr, 0, buffer.Length);
            return arr;
        }
        
        public (Array arr, int[] shape) GetDataAndShape()
        {
            Array arr = Array.CreateInstance(Numpy2CSharpDict[dtype], shape);
            byte[] buffer = Convert.FromBase64String(data);
            Buffer.BlockCopy(buffer, 0, arr, 0, buffer.Length);
            return (arr, shape);
        }
        
        public string PackJSON()
        {
            string json = JsonConvert.SerializeObject(this);
            return json;
        }

        public static PyUniValNdArray JsonConstructor(string jsonIn)
        {
            return JsonConvert.DeserializeObject<PyUniValNdArray>(jsonIn);
        }
        
        public override string ToString()
        {
            return $"{rpc_tag} of size {string.Join(",",shape)} with data type {dtype}";
        }

    }

    [Serializable, JsonConverter(typeof(StringEnumConverter))]
    public enum RpcTag
    {
        __RPC_CALL__, __RPC_RESULT__, __RPC_ERROR__, __RPC_VAL_NDARRAY__
    }
}
