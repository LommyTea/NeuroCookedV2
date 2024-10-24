using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEditor.PackageManager;
using Google.Protobuf.WellKnownTypes;
using Cysharp.Net.Http;
using Grpc.Net.Client;
using Grpc.Core;
using System;




public class Game_Behavior : MonoBehaviour
{
    

    private float flashInterval = 0.033f;       // Time between flashes
    private float waitBetweenSequences = 2f;  // Time to wait between sequences
    public int numberOfTrainingEpochs = 1;   // Number of training epochs
    public int[][] mSequences;
    private bool isFlashing = false;

    public Button startTrainingButton;               // The button to start flashing
    public GameObject flashingCube;          // The object that flashes (should be a GameObject)
    private Renderer cubeRenderer;           // Reference to the Renderer of the flashing cube

    public TMP_Text instructionsText;         // Text to hide when button is clicked
    public TMP_Text trainingModelText;
    public TMP_Text trainingCompleteText;
    public NeuroCookedRpcClient rpcClient;

    public Button startGameButton;         // The button to switch scenes after flashing






    void Start()
    {
        //Setting all of the variables/objects based on name in Unity
        GameObject.Find("Begin_Game").SetActive(false);
        GameObject.Find("Training_Cube").SetActive(false);
        rpcClient = FindObjectOfType<NeuroCookedRpcClient>();
        // Set up button to trigger flashing and hide elements when clicked
        startTrainingButton.onClick.AddListener(OnStartButtonClick);
        instructionsText.gameObject.SetActive(true);
        startTrainingButton.gameObject.SetActive(true);

        startGameButton.gameObject.SetActive(false);
        trainingModelText.gameObject.SetActive(false);
        trainingCompleteText.gameObject.SetActive(false);
        // Get the Renderer component from the flashing cube
        cubeRenderer = flashingCube.GetComponent<Renderer>();
        Debug.Log(rpcClient.Msequences);
        mSequences = rpcClient.Msequences;
        
    }


    void OnStartButtonClick()
    {
        if (!isFlashing)
        {
            // Hide the UI elements
            instructionsText.gameObject.SetActive(false);
            startTrainingButton.gameObject.SetActive(false);
            // Spawn and activate the cube
            flashingCube.SetActive(true);

            // Start the flashing process
            StartCoroutine(FlashTrainingSequences());
            StartCoroutine(waitingForTrainingStatus());


        }
    }
    IEnumerator FlashSequence(int[] sequence, int sequenceNumber)
    {
        float startTime = Time.time;  // Record start time for duration

        for (int i = 0; i < sequence.Length; i++)
        {
            // Set the object to black or white based on the m-sequence value (1 = white, 0 = black)
            cubeRenderer.material.color = sequence[i] == 1 ? Color.white : Color.black;

            // Wait for the next flash interval
            yield return new WaitForSeconds(flashInterval);
        }

        float endTime = Time.time;  // Record end time
        float duration = endTime - startTime;  // Calculate how long the sequence lasted

        // Call the RPC command to send the sequence number and duration (optional)
        rpcClient.AddSequenceData(sequenceNumber, duration);
        Debug.Log("Adding m-sequence" + sequenceNumber);

    }

    IEnumerator FlashTrainingSequences()
    {
        isFlashing = true;
        for (int epoch = 0; epoch < numberOfTrainingEpochs; epoch++)
        {
            for (int sequenceIndex = 0; sequenceIndex < mSequences.Length; sequenceIndex++)
            {
                // Flash the current sequence once
                yield return StartCoroutine(FlashSequence(mSequences[sequenceIndex], sequenceIndex+1));
                flashingCube.SetActive(false);

                // Wait between sequences
                yield return new WaitForSeconds(waitBetweenSequences);
                flashingCube.SetActive(true);

            }
        }

        isFlashing = false;
        // Keep the cube visible
        // Show the switch scene button after flashing is done
        //Call RPC that calls for training and removes flashing cube
        flashingCube.SetActive(false); 
        trainingModelText.gameObject.SetActive(true);
        rpcClient.trainingModel();
    }

    IEnumerator waitingForTrainingStatus()  
    {
        while (!rpcClient.training_status)
        {
            yield return null; // Wait until the next frame and check again
        }
        trainingModelText.gameObject.SetActive(false);
        trainingCompleteText.gameObject.SetActive(true);
        startGameButton.gameObject.SetActive(true);


    }

}
