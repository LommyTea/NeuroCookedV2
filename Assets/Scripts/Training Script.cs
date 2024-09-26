using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement; // For scene management

public class ButtonControlledMSequenceFlasher : MonoBehaviour
{
    public Button startTrainingButton;               // The button to start flashing
    public Button startGameButton;         // The button to switch scenes after flashing
    public Button failTrainingButton;
    public GameObject flashingCube;          // The object that flashes (should be a GameObject)
    public float flashInterval = 0.5f;       // Time between flashes
    public float waitBetweenSequences = 2f;  // Time to wait between sequences
    public int numberOfTrainingEpochs = 1;   // Number of training epochs
    public TMP_Text instructionsText;         // Text to hide when button is clicked
    public TMP_Text trainingText;            //Text to show when all epochs are done
    public TMP_Text failureText;



    private int[][] mSequences = new int[3][] {
        new int[] { 1, 0, 1, 0, 1 },  // Sequence 1
        new int[] { 0, 1, 0, 1, 0 },  // Sequence 2
        new int[] { 1, 1, 0, 0, 1 }   // Sequence 3
    };

    private bool isFlashing = false;
    private Renderer cubeRenderer;           // Reference to the Renderer of the flashing cube

    void Start()
    {
        // Set up button to trigger flashing and hide elements when clicked
        startTrainingButton.onClick.AddListener(OnStartButtonClick);

        // Get the Renderer component from the flashing cube
        cubeRenderer = flashingCube.GetComponent<Renderer>();
        // Ensure the flashing cube is initially inactive
        flashingCube.SetActive(false);
        // Ensure the scene switch button is initially inactive
        startGameButton.gameObject.SetActive(false);
        failTrainingButton.gameObject.SetActive(false);
        trainingText.gameObject.SetActive(false);
        failureText.gameObject.SetActive(false);
    }

    void OnStartButtonClick()
    {
        if (!isFlashing)
        {
            // Hide the UI elements
            instructionsText.gameObject.SetActive(false);
            startGameButton.gameObject.SetActive(false);

            // Spawn and activate the cube
            flashingCube.SetActive(true);

            // Start the flashing process
            StartCoroutine(FlashTrainingSequences());
        }
    }

    IEnumerator FlashTrainingSequences()
    {
        isFlashing = true;

        for (int epoch = 0; epoch < numberOfTrainingEpochs; epoch++)
        {
            for (int sequenceIndex = 0; sequenceIndex < mSequences.Length; sequenceIndex++)
            {
                // Flash the current sequence once
                yield return StartCoroutine(FlashSequence(mSequences[sequenceIndex], sequenceIndex + 1));

                // Wait between sequences
                yield return new WaitForSeconds(waitBetweenSequences);
            }
        }

        isFlashing = false;
        // Keep the cube visible
        // Show the switch scene button after flashing is done
        trainingText.gameObject.SetActive(true);
        //Call RPC that calls for training
        training();
        

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
        AddSequenceData(sequenceNumber, duration);

    }

    void AddSequenceData(int sequenceNumber, float duration)
    {
        // Placeholder for RPC command
        Debug.Log($"RPC: Sequence {sequenceNumber} finished. Duration: {duration} seconds.");

        // Here, you would replace this with your actual RPC command to notify the other program
    }
    
    void training()
    {
        int response = await; //training function
        if (response == 1)
        {
            startGameButton.gameObject.SetActive(true);
        }
        else
        {
            failTrainingButton.gameObject.SetActive(true);
            failureText.gameObject.SetActive(true);
        }
 
    }
}
