using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Google.Protobuf.WellKnownTypes;
using Cysharp.Net.Http;
using Grpc.Net.Client;
using Grpc.Core;
using LSL;

public class NeuralCookedRpcClient : MonoBehaviour
{
    //Singleton instance
    public static NeuralCookedRpcClient Instance { get; private set; }

    //Public variables for external access
    public int decoded_choice;

    //Setting up the RPC client
    private GrpcChannel channel;
    private YetAnotherHttpHandler handler;
    //private NeuralCooked.NeuralCookedClient client;
    private NeuralCooked.NeuralCookedClient client;

    public string host = "http://localhost:13004";

    //Creating event markers for offline analysis of EEG Data
    [Header("LSL Stream Settings")]
    public LSL.channel_format_t channelFormat = LSL.channel_format_t.cf_float32;
    public string streamName = "NeuroCookedEventMarker";
    public string streamType = "LSL";
    public int channelNum = 1;
    public float nominalSamplingRate = 100.0f;

    [Header("Stream Status")]
    public StreamOutlet streamOutlet;


    //Starting the RPC server as well as resetting the training status and decoded choice
    private void Awake()
    {
        //Ensure that only one instance of RpcClient exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); //This makes the object persist through scene loads
        }
        else
        {
            Destroy(gameObject); //Destroy any duplicate instances
        }
    }

    private void Start()
    {
        decoded_choice = 0;
        handler = new YetAnotherHttpHandler() { Http2Only = true };  //GRPC requires HTTP/2
        channel = GrpcChannel.ForAddress(host, new GrpcChannelOptions() { HttpHandler = handler, Credentials = ChannelCredentials.Insecure });
        //client = new NeuralCooked.NeuralCookedClient(channel);
        client = new NeuralCooked.NeuralCookedClient(channel);

        //create the stream outlet for troubleshooting purposes
        StreamInfo streamInfo = new StreamInfo(streamName,
                                        streamType,
                                        channelNum,
                                        nominalSamplingRate,
                                        channelFormat
                                        );
        streamOutlet = new StreamOutlet(streamInfo);
    }

    //Adding an RPC function calling physiolab to decode the model (but in reality it just grabs the most common decoded value)
    //Sets the decoded choice as whatever was decoded whenever it is called.
    public void StartDecode()
    {
        StartCoroutine(decodeSSVEP());
    }

    private IEnumerator decodeSSVEP()
    {
        var request = new Empty();
        var call = client.decodeSSVEPAsync(request);
        yield return new WaitUntil(() => call.ResponseAsync.IsCompleted);
        if (call.ResponseAsync.IsCompletedSuccessfully)
        {
            var response = call.ResponseAsync.Result;
            decoded_choice = response.Message;
        }
        else
        {
            Debug.Log("RPC call failed.");
        }
    }
}

