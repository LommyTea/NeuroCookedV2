using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Google.Protobuf.WellKnownTypes;
using Cysharp.Net.Http;
using Grpc.Net.Client;
using Grpc.Core;

public class NeuroCookedRpcClient : MonoBehaviour
{
    // Singleton instance
    public static NeuroCookedRpcClient Instance { get; private set; }

    // Public variables for external access
    public int decoded_choice;
    public bool training_status;

    // Setting up the RPC client
    private GrpcChannel channel;
    private YetAnotherHttpHandler handler;
    private NeuralCooked.NeuralCookedClient client;

    public string host = "http://localhost:13004";

    // Setting up the m-sequences for future use
    private int[][] mSequences = new int[3][] {
        new int[] { 1, 0, 1, 0, 1 },  // Sequence 1
        new int[] { 0, 1, 0, 1, 0 },  // Sequence 2
        new int[] { 1, 1, 0, 0, 1 }   // Sequence 3
    };

    // Starting the RPC server as well as resetting the training status and decoded choice
    private void Awake()
    {
        // Ensure that only one instance of RpcClient exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // This makes the object persist through scene loads
        }
        else
        {
            Destroy(gameObject); // Destroy any duplicate instances
        }
    }

    private void Start()
    {
        training_status = false;
        decoded_choice = 0;
        handler = new YetAnotherHttpHandler() { Http2Only = true };  // GRPC requires HTTP/2
        channel = GrpcChannel.ForAddress(host, new GrpcChannelOptions() { HttpHandler = handler, Credentials = ChannelCredentials.Insecure });
        client = new NeuralCooked.NeuralCookedClient(channel);
    }

    // Allows for other codes to access the m-sequences without needing to create bloat
    public int[][] Msequences
    {
        get { return mSequences; }
    }

    // Adding an RPC function calling for physiolab to collect all of the data from a certain duration past
    // and add the EEG data under the sequence it corresponds to
    // Returns nothing
    public void AddSequenceData(int sequenceNumber, float duration)
    {
        AddSequenceDataRPC(sequenceNumber, duration);
    }

    private void AddSequenceDataRPC(int sequenceNumber, float duration)
    {
        var request = new add_seq_dataRequest() { SequenceNum = sequenceNumber, Duration = duration };
        var call = client.add_seq_dataAsync(request);
    }

    // Adding an RPC function calling physiolab to train the model based on all of the data it has.
    // Sets training_status to true once it is done training.
    public void trainingModel()
    {
        StartCoroutine(training());
    }

    private IEnumerator training()
    {
        Debug.Log("Calling Training");
        var request = new Empty();
        var call = client.trainingAsync(request);
        Debug.Log("Called RPC");
        yield return new WaitUntil(() => call.ResponseAsync.IsCompleted);
        if (call.ResponseAsync.IsCompletedSuccessfully)
        {
            var response = call.ResponseAsync.Result;
            Debug.Log(response.ToString());
            if (response.Message == 1)
            {
                training_status = true;
            }
        }
    }

    // Adding an RPC function calling physiolab to decode the model (but in reality it just grabs the most common decoded value)
    // Sets the decoded choice as whatever was decoded whenever it is called.
    public void StartDecode()
    {
        StartCoroutine(decode());
    }

    private IEnumerator decode()
    {
        Debug.Log("Calling decode");
        var request = new Empty();
        var call = client.decodeAsync(request);
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

