import numpy as np
import base64
from binascii import Error as BinAsciiError
import json
import zmq

class RPCHandles:
    """Holds a list of handles that can be called by a RPC request."""
    #Inspired by https://github.com/bcb/jsonrpcserver/blob/master/jsonrpcserver/methods.py

    def __init__(self, *args, **kwargs):
        self.items = {}
        self.add(*args, **kwargs)

    def __getitem__(self, k):
        return self.items[k]
    
    def keys(self): return self.items.keys()
    
    def add(self, *args, **kwargs):
        for m in args: assert callable(m)
        for _, m in kwargs.items(): assert callable(m)
        self.items = {
            **self.items,
            **{m.__name__: m for m in args},
            **{k: v for k, v in kwargs.items()},
        }
        if len(args): return args[0]
        return None
    
    def call(self, __rpc_handle_name, *args, **kwargs):
        #Call a registered function by name
        func = self.items[__rpc_handle_name]
        return func(*args,**kwargs)
    
global_rpchandles = RPCHandles()

def rpchandle(*args, **kwargs):
    '''
    Decorator, used to add a function to the list of RPC handles.
    Example:
     @rpchandle
     def my_function():
        return "hello"
    '''
    return global_rpchandles.add(*args, **kwargs)


def rpcarg2ndarray(dtype, shape, data, b64=True, **unused):
    #Convert a JSON encoded np.ndarray to an python object
    if isinstance(dtype, str): dtype = np.dtype(dtype)
    if b64:
        try: data = base64.b64decode(data)
        except BinAsciiError: raise ValueError("`data` was not a valid Base64 string") from None
    return np.frombuffer(data, dtype=dtype).reshape(shape)

def ndarray2rpcarg(a, dtype=None, b64=True):
    #Convert a np.ndarray to a JSON-compatible dict
    if dtype is None: dtype = a.dtype.name
    else: a = a.astype(dtype)
    data = a.data if a.flags['C_CONTIGUOUS'] else np.ascontiguousarray(a).data
    if b64: data = base64.b64encode(data).decode("utf-8")
    return {
        'rpc_tag': '__RPC_VAL_NDARRAY__',
        'dtype':dtype,
        'shape':a.shape,
        'data': data
    }


def decode_val(arg, **kwargs):
    #Decode a value (Dict->Object), if applicable
    if isinstance(arg,dict) and 'rpc_tag' in arg.keys(): #Check for special arg
        tag = arg['rpc_tag']
        assert tag in arg_decoders.keys(), f"Unknown RPC arg decoder tag '{tag}'."
        decoder = arg_decoders[tag] 
        return decoder(**arg, **kwargs)
    else:
        return arg

def encode_val(arg, **kwargs):
    #Encode a value (Object->Dict), if applicable
    for k in arg_encoders.keys():
        if isinstance(arg, k):
            encoder = arg_encoders[k]
            return encoder(arg, **kwargs)
    return arg

def encode_vals(vals):
    #Encode multiple values
    return [encode_val(val) for val in vals]

def decode_vals(vals):
    #Decode multiple values
    return [decode_val(val) for val in vals]

def encode_args(args, kwargs):
    #Encode positional and keyword arguments
    args = encode_vals(args)
    kwargs = {k:encode_val(v) for k,v in kwargs.items()}
    return args, kwargs
    
def decode_args(args, kwargs):
    #Decode positional and keyword arguments
    args = [decode_val(arg) for arg in args]
    kwargs = {k:decode_val(v) for k,v in kwargs.items()}
    return args, kwargs

def dispatch(rpc_tag=None,handle=None,uid=None, args=[], kwargs={}, handles=global_rpchandles):
    #Handle RPC call
    assert rpc_tag == '__RPC_CALL__', "Invalid RPC call: invalid tag." #Safety check
    assert uid is not None, "Invalid RPC call: invalid uid."
    assert handle in handles.keys(), f"Invalid RPC call: unkown handle `{handle}`."
    #Transform special args and kwargs
    args, kwargs = decode_args(args, kwargs)
    #Call function and return
    res = handles.call(handle, *args, **kwargs)
    if not isinstance(res, tuple): res = (res,)
    return prepare_result(res, handle, uid)
    #except Exception as e:
    #    return prepare_error(e, handle, uid)

def prepare_call(handle, uid, args=[], kwargs={}):
    #Prepare a call dictionary
    args, kwargs = encode_args(args, kwargs)
    res = {
        'rpc_tag': '__RPC_CALL__',
        'handle': handle,
        'uid': uid,
        'args': args,
        'kwargs': kwargs
    }
    return res
    
    
def prepare_result(vals=[],handle=None, uid=None):
    #Prepary a result dictionary
    assert uid is not None, "Invalid uid."
    
    res = {
        'rpc_tag': '__RPC_RESULT__',
        'handle': handle,
        'uid': uid,
        'result': encode_vals(vals),
    }
    return res

def prepare_error(e,handle=None, uid=None):
    #Prepary a error dictionary
    res = {
        'rpc_tag': '__RPC_ERROR__',
        'handle': handle,
        'uid': uid,
        'exception': type(e).__name__,
        'descr': str(e),
    }
    return res

def attempt(fun):
    try: fun()
    except: pass

def rpcserver(handles=global_rpchandles, bind="tcp://*:6789", verbose=False):
    '''
      Start a PyUniRPC server.
      
      Parameters
      ----------
      handles : RPCHandles
        Holds handles to RPC runctions. Default: pyunirpc.global_rpchandles
      bind : String
        ZMQ bind address. Default: "tcp://*:6789"
      verbose : Bool
        Default: False
    '''
    try:
        ctx = zmq.Context()
        sock = ctx.socket(zmq.REP)
        sock.setsockopt(zmq.LINGER, 0)
        sock.bind(bind)
        if verbose: print(f"Listening on {bind}")

        while True:
            handle, uid = None,None
            json_msg = sock.recv()
            try:
                msg = json.loads(json_msg)
                if verbose: print("DISPATCHING CALL", msg)
                res = dispatch(**msg, handles=handles)
                if verbose: print("RETURNING RESULT", res)
                sock.send(json.dumps(res).encode())
            except Exception as e:
                try: handle, uid = msg.get('handle'), msg.get('uid')
                except: pass
                err = prepare_error(e, handle, uid)
                if verbose: print("RETURNING ERROR:", err)
                sock.send(json.dumps(err).encode())
    except:
        attempt(sock.close)
        attempt(ctx.destroy)
        raise


#Special argument decoders
arg_decoders = {
    '__RPC_VAL_NDARRAY__': rpcarg2ndarray, #Handles np.ndarrays
}

#Special argument encoders
arg_encoders = {
    np.ndarray: ndarray2rpcarg, #Handles np.ndarrays
}
