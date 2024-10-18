using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SocialPlatforms.Impl;

public class Game : MonoBehaviour
{
    private float flashInterval = 0.033f;  // Time between flashes (~30Hz)
    public int[][] mSequences;             // Holds the m-sequences
    public GameObject[] cubes;             // Array of cubes to flash
    public TextMeshPro choiceText;         // Reference to the 3D TextMeshPro element
    public TextMeshPro timerText;          // Reference to the TextMeshPro element for the timer
    public int decodedChoice;
    public int decodedItem;
    public int points;
    public float gameDuration = 30f;       // Set the game duration (e.g., 30 seconds)
    private List<List<int>> itemPool = new List<List<int>>()
    {
        new List<int> { 1, 2, 1, 1 },
        new List<int> { 1, 2, 3, 1 },
        new List<int> { 1, 3, 3, 1 }
    };  // Pool of choices
    private List<int> currentItem;
    private List<int> chosenItems = new List<int>();
    private int flashIndex = 0;
    private float decodeInterval = 0.5f;
    public rpcClient rpcClient;
    private Coroutine decodeCoroutine;
    public TextMeshPro chosenItemsText;
    public TextMeshPro score;

    // Start is called before the first frame update
    void Start()
    {
        // Initialize the m-sequences
        mSequences = rpcClient.Instance.Msequences;
        rpcClient = FindObjectOfType<rpcClient>();
        points = 0;
        score.text = "Points";
        choiceText.text = "Customer Order";
        timerText.text = "Timer";
        chosenItemsText.text = "Added Items";
        // Display a random choice for the user
        SetRandomChoice();

        // Assign the button's onClick event to the method that reassigns a choice
        SetRandomChoice();
        // Start the flashing process
        InvokeRepeating("FlashCubes", 0, flashInterval);  // Calls FlashCubes every 0.033 seconds

        // Start the decode process to run every few milliseconds (500ms in this case)
        decodeCoroutine = StartCoroutine(DecodeAtIntervals(decodeInterval));

        // Start the game timer to stop everything after a set duration
        StartCoroutine(GameTimer(gameDuration));

        // Start the timer display coroutine to update the remaining time on the screen
        StartCoroutine(UpdateTimerDisplay());
    }

    // Set a random choice from the pool and display it in the text element
    void SetRandomChoice()
    {
        currentItem = itemPool[Random.Range(0, itemPool.Count)];  // Get the choice based on the random index

        choiceText.text = string.Join(", ", currentItem);  // Update the 3D TextMeshPro element to show the random choice
    }


    // Method to flash cubes based on their m-sequences
    void FlashCubes()
    {
        for (int i = 0; i < cubes.Length; i++)
        {
            // Get the current m-sequence for the cube
            int[] currentSequence = mSequences[i];

            // If the current flash index is 0, flash white; if it's 1, flash black
            if (currentSequence[flashIndex % currentSequence.Length] == 0)
            {
                cubes[i].GetComponent<Renderer>().material.color = Color.white;  // Flash white
            }
            else
            {
                cubes[i].GetComponent<Renderer>().material.color = Color.black;  // Flash black
            }
        }

        // Increment the flash index to move to the next part of the m-sequence
        flashIndex++;
    }

    // Coroutine to run the decode RPC call at intervals
    IEnumerator DecodeAtIntervals(float interval)
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);  // Wait for the specified interval (in seconds)

            if (rpcClient != null)
            {
                if (rpcClient.Instance != null)
                {
                    rpcClient.Instance.StartDecode();
                    Debug.Log($"Decoded choice: {rpcClient.decoded_choice}");
                    decodedChoice = rpcClient.decoded_choice;

                    if (decodedChoice != 0)
                    {
                        if (chosenItems.Count == currentItem.Count)
                        {
                            if (AreListsEqual(currentItem, chosenItems))
                            {
                                points++;
                                chosenItems.Clear();
                                SetRandomChoice();
                            }
                            else { chosenItems.Clear();}
                        }
                        else 
                        {
                            chosenItems.Add(decodedChoice);
                            chosenItemsText.text = "Chosen Items: " + string.Join(", ", chosenItems); // Update the display
                        }
                        score.text = points.ToString();
                    }
                }
                else { Debug.LogError("rpcClient.Instance is null.");}
            }
            else { Debug.LogError("rpcClient is null.");}
        }
    }

    // Method to check if two lists are equal
    bool AreListsEqual(List<int> list1, List<int> list2)
    {
        if (list1.Count != list2.Count) return false;

        for (int i = 0; i < list1.Count; i++)
        {
            if (list1[i] != list2[i])
            {
                return false;
            }
        }

        return true;
    }

    // Timer coroutine that stops all tasks after a set duration
    IEnumerator GameTimer(float duration)
    {
        yield return new WaitForSeconds(duration);  // Wait for the duration of the game

        // Stop the flashing and decoding
        CancelInvoke("FlashCubes");
        if (decodeCoroutine != null)
        {
            StopCoroutine(decodeCoroutine);
        }

        // Optional: Display message indicating the game is over
        choiceText.text = "Game Over. Points: " + points;
        Debug.Log("Game ended. Total points: " + points);
    }

    // Coroutine to update the timer display every second
    IEnumerator UpdateTimerDisplay()
    {
        float timeRemaining = gameDuration;

        while (timeRemaining > 0)
        {
            timerText.text = "Time Left: " + timeRemaining.ToString("0.0") + "s";  // Update the timer display

            yield return new WaitForSeconds(0.1f);  // Wait for a small amount of time (updates ~10 times per second)

            timeRemaining -= 0.1f;  // Decrease the time remaining
        }

        // Ensure that the timer reaches 0 and stops
        timerText.text = "Time Left: 0.0s";
    }
}

