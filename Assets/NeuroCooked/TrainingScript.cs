using System.Collections;
using System.Collections.Generic;
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
    

    private float flashInterval = 0.033f;           //Time between flashes This will need to change based on the monitor used for testing as well as the change in sampling frequency.
    private float waitBetweenSequences = 2f;        //Time to wait between sequences
    public int numberOfTrainingEpochs = 1;          //Number of training epochs
    public int[][] mSequences;                      //Creates an array for the m-sequences
    private bool isFlashing = false;                //Keeps track of if the flashing is happening or not

    public Button startTrainingButton;              //The button to start flashing
    public GameObject flashingCube;                 //The object that flashes (should be a GameObject)
    private Renderer cubeRenderer;                  //Reference to the Renderer of the flashing cube

    public TMP_Text instructionsText;               //Text to hide when button is clicked
    public TMP_Text trainingModelText;              //Text to show that the model is training
    public TMP_Text trainingCompleteText;           //Text to show that the training is complete
    public NeuralCookedRpcClient rpcClient;         //RPC client

    public Button startGameButton;                  //The button to switch scenes after flashing


    void Start()
    {
        //Setting all of the variables/objects based on name in Unity
        GameObject.Find("Begin_Game").SetActive(false);
        GameObject.Find("Training_Cube").SetActive(false);
        rpcClient = FindObjectOfType<NeuralCookedRpcClient>();
        //Set up button to trigger flashing and hide elements when clicked
        startTrainingButton.onClick.AddListener(OnStartButtonClick);
        instructionsText.gameObject.SetActive(true);
        startTrainingButton.gameObject.SetActive(true);

        startGameButton.gameObject.SetActive(false);
        trainingModelText.gameObject.SetActive(false);
        trainingCompleteText.gameObject.SetActive(false);
        //Get the Renderer component from the flashing cube
        cubeRenderer = flashingCube.GetComponent<Renderer>();
        Debug.Log(rpcClient.Msequences);
        mSequences = rpcClient.Msequences;


    }

    //The overarching function for starting training as well as the training of the model
    void OnStartButtonClick()
    {
        if (!isFlashing)
        {
            //Hide the UI elements
            instructionsText.gameObject.SetActive(false);
            startTrainingButton.gameObject.SetActive(false);
            //Spawn and activate the cube
            flashingCube.SetActive(true);

            //Start the flashing process
            StartCoroutine(FlashTrainingSequences());
            StartCoroutine(waitingForTrainingStatus());


        }
    }

    //Function to flash the m-sequence based on what number is provided and the m-sequence itself
    IEnumerator FlashSequence(int[] sequence, int sequenceNumber)
    {
        float startTime = Time.time;  //Record start time for duration
        for (int i = 0; i < sequence.Length; i++)
        {
            //Set the object to black or white based on the m-sequence value (1 = white, 0 = black)
            cubeRenderer.material.color = sequence[i] == 0 ? Color.white : Color.black;

            //Wait for the next flash interval
            yield return new WaitForSeconds(flashInterval);
        }

        float endTime = Time.time;  //Record end time
        float duration = endTime - startTime;  //Calculate how long the sequence lasted

        //Call the RPC command to send the sequence number and duration (optional)
        rpcClient.AddSequenceData(sequenceNumber, duration);
        Debug.Log("Adding m-sequence" + sequenceNumber);

    }
    //Function that is the training process, repeating the alternating flashing for a number of epochs defined by user
    IEnumerator FlashTrainingSequences()
    {
        isFlashing = true;

        //Create a list of sequence indices
        List<int> sequenceIndices = new List<int>();
        for (int i = 0; i < mSequences.Length; i++)
        {
            sequenceIndices.Add(i);
        }

        for (int epoch = 0; epoch < numberOfTrainingEpochs; epoch++)
        {
            //Shuffle the sequence indices for random order
            for (int i = 0; i < sequenceIndices.Count; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, sequenceIndices.Count);
                int temp = sequenceIndices[i];
                sequenceIndices[i] = sequenceIndices[randomIndex];
                sequenceIndices[randomIndex] = temp;
            }

            //Flash sequences in random order
            for (int i = 0; i < sequenceIndices.Count; i++)
            {
                int sequenceIndex = sequenceIndices[i];
                rpcClient.streamOutlet.push_sample(new float[] { (float)sequenceIndex });

                //Flash the current sequence once
                yield return StartCoroutine(FlashSequence(mSequences[sequenceIndex], sequenceIndex + 1));
                flashingCube.SetActive(false);

                //Wait between sequences
                yield return new WaitForSeconds(waitBetweenSequences);
                flashingCube.SetActive(true);
                rpcClient.streamOutlet.push_sample(new float[] { -(float)sequenceIndex });
            }
        }

        isFlashing = false;

        //Keep the cube visible
        //Show the switch scene button after flashing is done
        //Call RPC that calls for training and removes flashing cube
        flashingCube.SetActive(false);
        trainingModelText.gameObject.SetActive(true);
        rpcClient.trainingModel();
    }

    //Function to allow for Unity to wait for the asynchronous RPC function training to finish
    IEnumerator waitingForTrainingStatus()  
    {
        while (!rpcClient.training_status)
        {
            yield return null; //Wait until the next frame and check again
        }
        trainingModelText.gameObject.SetActive(false);
        trainingCompleteText.gameObject.SetActive(true);
        startGameButton.gameObject.SetActive(true);


    }

}
