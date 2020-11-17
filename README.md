# PyUniRPC (pronounced puny-rpc)
Søren Rasmussen [soren.rasmussen@alexandra.dk](mailto:soren.rasmussen@alexandra.dk)  
Oliver Gyldenberg Hjermitslev [oliver.gyldenberg@alexandra.dk](mailto:oliver.gyldenberg@alexandra.dk)

A tiny RPC framework, intended for calling Python functionality from Unity, using JSON signalling over ZMQ.

For an example of how to set up a Python RPC server and make a simple request from Python, refer to `rpc_server.ipynb` and `rpc_client.ipynb`.

RPC call structure
------------------
Messaging is done via ZMQ (Request-Reply) using JSON encoded messages.

The structure of a call is as follows:

    {
        rpc_tag: '__RPC_CALL__', #Marks this message as a RPC call
        handle: 'handle_name',   #Tells the server which function we're calling
        uid: 1234,               #A marker used to recognize the response to this call
        args: <args>,            #List of positional arguments
        kwargs: <kwargs>         #Dict of keyword arguments
    }

JSON-unfriendly argument- and keyword argument types are converted to a JSON-friendly format for transmission and then back again.
Conversion functions are rigistered in the the argument-encoder and -decoder dictionaries, `pyunirpc.arg_encoders` and `pyunirpc.arg_decoders`.

The argument encoder keeps track of special object types and the functions to convert them to JSON.
By default, the only registered encoder is `np.ndarray: ndarray2rpcarg`, which handles Numpy arrays.
Objects are encoded as dictionaries, holding the necessary data. An example for a Numpy array:

    {
        'rpc_tag': '__RPC_VAL_NDARRAY__', #Unique tag for this type (ndarray in this case)
        'dtype': dtype,                   #Needed to re-create the array
        'shape': shape,                   #Needed to re-create the array
        'data': data_base64               #Base-64 endcoded raw data.
    }
    
The argument decoder looks through (keyword-)arguments for dictionaries with a `rpc_tag` key which matches a key in the decoder dictionary and uses the corresponding function to transform the JSON dictionary to a Python object.
By default, the only registered decoder is `'__RPC_VAL_NDARRAY__': rpcarg2ndarray`.

Reserved tags:
--------------
There are a few reserved tags, which cannot be used by custom encoder/decoders.
These are:

    __RPC_CALL__
    __RPC_RESULT__
    __RPC_ERROR__
    
Adding to Unity Projects
------------------------
The source includes the Unity project and a .unitypackage. In the Unity editor, import the .unitypackage (Assets->Import Package->Custom Package...)

This package includes a scene labelled "SampleScene" and a Test.cs script, which illustrates basic use of the plugin.

This sample scene illustrates communication between the Unity client and a python server running on localhost:6789

The REQ/RES structure, and thus the rpc tags, exist as separate classes which derive from a common PyUniType. All further distinction requires safe casting to e.g. PyUniCall, PyUniError, etc.

Deserializing the JSON with .JsonConstructor<T> attempts, iteratively, to deserialize included PyUniValNdArrays to reduce manual conversions. 
  
Data from PyUniValNdArrays can be collected as generic Array type using .GetData and casted. It is recommended to keep a reference to this data alive as long as necessary, as the conversion can be costly.
