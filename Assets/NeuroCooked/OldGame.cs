using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SocialPlatforms.Impl;

public class Game : MonoBehaviour
{
    //Inner workings of the game
    private float flashInterval = 0.033f;  // Time between flashes (~30Hz)
    public int[][] mSequences;             // Holds the m-sequences
    public GameObject[] cubes;             // Array of cubes to flash
    public int decodedChoice;
    public int decodedItem;
    public int points;
    public float gameDuration = 30f;       // Set the game duration (e.g., 30 seconds)
    private int flashIndex = 0;
    private float decodeInterval = 0.5f;
    public rpcClient rpcClient;
    private Coroutine decodeCoroutine;

    //GUI or actual game elements
    public TextMeshPro chosenItemsText;
    public TextMeshPro score;
    public Transform[] orderLocation;
    public GameObject[] foodPrefabs;
    private GameObject[] currentFoodItems;
    private List<int> currentItem;
    private List<int> userChosenItems = new List<int>();
    public TextMeshPro choiceText;         // Reference to the 3D TextMeshPro element
    public TextMeshPro timerText;          // Reference to the TextMeshPro element for the timer

    private Dictionary<int, GameObject> foodPrefabDictionary;

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
        SetCustomerOrder();

        // Start the flashing process
        InvokeRepeating("FlashCubes", 0, flashInterval);  // Calls FlashCubes every 0.033 seconds

        // Start the decode process to run every few milliseconds (500ms in this case)
        decodeCoroutine = StartCoroutine(DecodeAtIntervals(decodeInterval));

        // Start the game timer to stop everything after a set duration
        StartCoroutine(GameTimer(gameDuration));

        currentFoodItems = new GameObject[orderLocation.Length];
        ShuffleFoodAssignments();
    }

    // Set a random choice from the pool and display it in the text element
    void SetCustomerOrder()
    {
        currentItem = GenerateRandomSequence(1, 3, 3);  // Get the choice based on the random index

        // Update the 3D TextMeshPro element to show the random choice
        choiceText.text = string.Join(", ", currentItem);

        // Clear previous food items (optional)
        foreach (Transform child in orderLocation)
        {
            Destroy(child.gameObject);
        }

        // Instantiate the food prefabs based on the currentItem array
        foreach (int index in currentItem)
        {
            // Make sure the index is within the range of the foodPrefabs array
            if (index - 1 >= 0 && index - 1 < foodPrefabs.Length)
            {
                GameObject foodPrefab = foodPrefabs[index - 1];  // Adjust for 0-based index
                Instantiate(foodPrefab, orderLocation[index - 1].position, Quaternion.identity);
            }
        }
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
                        if (userChosenItems.Count == currentItem.Count)
                        {
                            if (AreListsEqual(currentItem, userChosenItems))
                            {
                                points++;
                                userChosenItems.Clear();
                                SetCustomerOrder();
                            }
                            else { userChosenItems.Clear(); }
                        }
                        else
                        {
                            userChosenItems.Add(decodedChoice);
                            //userChosenItemsText.text = "Chosen Items: " + string.Join(", ", userChosenItems); // Update the display
                        }
                        score.text = points.ToString();
                    }
                }
                else { Debug.LogError("rpcClient.Instance is null."); }
            }
            else { Debug.LogError("rpcClient is null."); }
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

    // Gametimer coroutine that updates the display and stops all tasks after a set duration
    IEnumerator GameTimer(float duration)
    {
        float timeRemaining = duration;

        // Loop until the time runs out
        while (timeRemaining > 0)
        {
            // Update the timer display every 0.1 seconds
            timerText.text = "Time Left: " + timeRemaining.ToString("0.0") + "s";

            // Wait for a short amount of time (0.1 seconds)
            yield return new WaitForSeconds(0.1f);

            // Decrease the remaining time
            timeRemaining -= 0.1f;
        }

        // Ensure the timer reaches exactly 0.0 seconds
        timerText.text = "Time Left: 0.0s";

        // Stop the flashing and decoding once time is up
        CancelInvoke("FlashCubes");
        if (decodeCoroutine != null)
        {
            StopCoroutine(decodeCoroutine);
        }

        // Optional: Display a message indicating the game is over
        choiceText.text = "Game Over. Points: " + points;
        Debug.Log("Game ended. Total points: " + points);
    }
    public List<int> GenerateRandomSequence(int min, int max, int length)
    {
        List<int> sequence = new List<int>();

        // Create a list of all possible numbers
        List<int> numbers = new List<int>();
        for (int i = min; i <= max; i++)
        {
            numbers.Add(i);
        }

        // Shuffle the list and take the first 'length' numbers for the sequence
        for (int i = 0; i < length; i++)
        {
            int randomIndex = Random.Range(0, numbers.Count);
            sequence.Add(numbers[randomIndex]);
            numbers.RemoveAt(randomIndex);  // Ensure uniqueness by removing the selected number
        }

        return sequence;
    }
    public void ShuffleFoodAssignments()
    {
        foodPrefabDictionary = AssignRandomNumbersToFoods(foodPrefabs);

        // Print the assignments for verification
        foreach (KeyValuePair<int, GameObject> entry in foodPrefabDictionary)
        {
            Debug.Log("Number: " + entry.Key + ", Food Prefab: " + entry.Value.name);
        }
    }
    private Dictionary<int, GameObject> AssignRandomNumbersToFoods(GameObject[] foods)
    {
        Dictionary<int, GameObject> assignments = new Dictionary<int, GameObject>();
        List<int> numbers = new List<int>();

        // Create a list of numbers from 1 to the number of foods
        for (int i = 1; i <= foods.Length; i++)
        {
            numbers.Add(i);
        }

        // Shuffle the list of numbers
        for (int i = 0; i < numbers.Count; i++)
        {
            int randomIndex = Random.Range(0, numbers.Count);
            // Swap the numbers[i] with numbers[randomIndex]
            int temp = numbers[i];
            numbers[i] = numbers[randomIndex];
            numbers[randomIndex] = temp;
        }

        // Assign the shuffled numbers to the food prefabs
        for (int i = 0; i < foods.Length; i++)
        {
            assignments.Add(numbers[i], foods[i]);
        }

        return assignments;
    }
    void SpawnUniqueFoodAtAllLocations()
    {
        // Clear existing food items
        for (int i = 0; i < currentFoodItems.Length; i++)
        {
            if (currentFoodItems[i] != null)
            {
                Destroy(currentFoodItems[i]);
            }
        }

        // Loop through spawn locations
        for (int i = 0; i < orderLocation.Length; i++)
        {
            // Get the food prefab associated with the spawn location
            int index = i + 1; // Convert to 1-based index
            if (foodPrefabDictionary.ContainsKey(index))
            {
                GameObject foodPrefab = foodPrefabDictionary[index];

                // Instantiate the new food item at the current spawn location
                GameObject newFood = Instantiate(foodPrefab, orderLocation[i].position, Quaternion.identity);

                // Store reference to the newly spawned food item
                currentFoodItems[i] = newFood;
            }
        }
    }
}

