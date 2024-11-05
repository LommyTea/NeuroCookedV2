using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SocialPlatforms.Impl;
using System.Linq;
using UnityEngine.UIElements;
using System;

public class MainGame : MonoBehaviour
{
    //Game Constants
    private float flashInterval = 0.033f;   //time between flashes (~30Hz)
    public float gameDuration = 600f;        //Duration of the game: 30 seconds
    private float decodeInterval = 0.5f;    //How often does it decode: 500 ms
    public NeuralCookedRpcClient rpcClient;
    public int[][] mSequences;              //m-sequence holder
    public AudioSource audioSource;
    public AudioClip audioClip;

    //Game Locations
    public Transform[] orderLocation;       //Where the order will show up
    public Transform[] userChosenLocation;  //Where the user's choices will show up
    public Transform[] foodLocation;        //Where the food will show up

    //Game rules
    public TextMeshPro score;
    public TextMeshPro timer;
    public int points;

    //Food elements
    public GameObject[] foodItems;                          //Prefabs for the foods
    private int ChosenItem;                                 //What has been decoded most recently
    private int orderFood;                                   //Indicates what is the ordered food for that number the user is on
    private List<int> orderFoodItems = new List<int>();     //What has been ordered
    private List<int> userChosenItems = new List<int>();    //What has been decoded for the order
    private Dictionary<int, GameObject> foodDictionary;     //Dictionary for assigning prefabs to items on table
    private List<GameObject> instantiatedOrderItems = new List<GameObject>();
    private List<GameObject> instantiatedUserChosenItems = new List<GameObject>();
    private List<GameObject> instantiatedFoodItems = new List<GameObject>();

    //Flashing Cubes
    public GameObject[] cubes;                              //Flashing Cubes
    private int flashIndex = 0;
    private Coroutine decodeCoroutine;


    void Start()
    {
        //Initialize all variables
        rpcClient = FindObjectOfType<NeuralCookedRpcClient>();
        mSequences = rpcClient.Msequences;
        points = 0;
        score.text = "Points";
        timer.text = "Timer";
        audioSource = gameObject.gameObject.AddComponent<AudioSource>();

        //Start with setting the customer order
        SetCustomerOrder();
        //Start the flashing of the cubes
        InvokeRepeating("FlashCubes", 0, flashInterval);  // Calls FlashCubes every 0.033 seconds
        //Start the decoding
        decodeCoroutine = StartCoroutine(DecodeAtIntervals(decodeInterval));
        //Start the game timer
        StartCoroutine(GameTimer(gameDuration));
        //Update item order
        //Update item 
        SetCustomerOrder();
        ClearBoard();
        //set up board
        updateBoard();
    }

    //Game timer function
    IEnumerator GameTimer(float duration)
    {
        float timeRemaining = duration;
        while (timeRemaining > 0)
        {
            // Update the timer display every 0.1 seconds
            timer.text = "Time Left: " + timeRemaining.ToString("0.0") + "s";
            yield return new WaitForSeconds(0.1f);
            timeRemaining -= 0.1f;
        }
        timer.text = "Time Left: 0.0s";

        // Stop the flashing and decoding once time is up
        CancelInvoke("FlashCubes");
        if (decodeCoroutine != null)
        {
            StopCoroutine(decodeCoroutine);
        }
        timer.text = "Game Ended.";
    }

    //Flashing Cubes function
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

    //Decoding function
    IEnumerator DecodeAtIntervals(float interval)
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);  // Wait for the specified interval (in seconds)

            if (rpcClient != null)
            {
                if (rpcClient != null)
                {
                    rpcClient.StartDecode();
                    Debug.Log($"Decoded choice: {rpcClient.decoded_choice}");
                    ChosenItem = rpcClient.decoded_choice;

                    if (ChosenItem != 0)
                    {
                        userChosenItems.Add(ChosenItem);    //add the chosen item to the list
                        updateBoard();

                        if (userChosenItems.Count == orderFoodItems.Count)
                        {
                            //Compare to see if the orderedFood Items are the same as what the user has chosen
                            if (AreListsEqual(orderFoodItems, userChosenItems))
                            {
                                //If the two lists are equal player correctly got the right items
                                //add a point, clear the chosen items, and reset what the customer wants
                                Debug.Log(userChosenItems);
                                points++;
                                userChosenItems.Clear();
                                SetCustomerOrder();
                                audioSource.PlayOneShot(audioClip);

                                yield return new WaitForSeconds(0.02f);
                                updateBoard();
                                //clear userChosenLocation
                            }
                            //if it is not equal, clear the chosen items and do nothing
                            else 
                            { 
                                userChosenItems.Clear(); 
                                yield return new WaitForSeconds(0.02f);
                                updateBoard(); 
                            }
                        }
                        
                        //updating the score
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
            if (list1[i] != list2[i]) return false;
        }
        return true;
    }
    List<int> generateRandom(int x, int y, int z)
    {
        HashSet<int> randomValuesSet = new HashSet<int>();

        while (randomValuesSet.Count < 3)
        {
            int randomValue = UnityEngine.Random.Range(x, y + 1);
            if (randomValue != z)
            {
                randomValuesSet.Add(randomValue); // Only add if it's unique and not equal to z
            }
        }

        // Convert the HashSet to a List
        List<int> randomValues = randomValuesSet.ToList();
        return randomValues; // Return the list with three unique values
    }


    void SetCustomerOrder()
    {
        //Randomize the foods within the foodItems array and create a dicitonary for it
        foodDictionary = AssignRandomNumbersToFoods(foodItems);

        // Print the assignments for verification
        foreach (KeyValuePair<int, GameObject> entry in foodDictionary)
        {
            Debug.Log("Number: " + entry.Key + ", Food Prefab: " + entry.Value.name);
        }
        orderFoodItems = GenerateRandomSequence(1, 3, 3);  // Get the choice based on the random index

    }
    public List<int> GenerateRandomSequence(int min, int max, int length)
    {
        // Generate the list of numbers and shuffle them
        return Enumerable.Range(min, max - min + 1)
                         .OrderBy(x => UnityEngine.Random.Range(0, max))
                         .Take(length)
                         .ToList();
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
            int randomIndex = UnityEngine.Random.Range(0, numbers.Count);
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

    //Spawn the food at the locations
    void updateBoard()
    {
        ClearBoard();
        // Spawn the ordered food based on orderFoodItems
        int foodIndex = 0;
        foreach (int item in orderFoodItems)
        {
            GameObject foodItem = foodDictionary[item];
            GameObject instantiatedItem = Instantiate(foodItem, orderLocation[foodIndex].position, Quaternion.identity);
            instantiatedOrderItems.Add(instantiatedItem); // Store the instantiated object
            foodIndex++;
        }

        // Instantiate the user's chosen food items
        int userIndex = 0;
        foreach (int item in userChosenItems)
        {
            GameObject foodItem = foodDictionary[item];
            GameObject instantiatedItem = Instantiate(foodItem, userChosenLocation[userIndex].position, Quaternion.identity);
            instantiatedUserChosenItems.Add(instantiatedItem); // Store the instantiated object
            userIndex++;
        }

        //Instantiate random food items

        // Make sure userChosenItems.Count is within bounds
        if (userChosenItems.Count < orderFoodItems.Count)
        {
            // Instantiate the food item that the customer has to choose (next order food)
            orderFood = orderFoodItems[userChosenItems.Count];  // Get the next order food item

            GameObject food = foodDictionary[orderFood];
            GameObject instantiatedItem = Instantiate(food, foodLocation[orderFood - 1].position, Quaternion.identity);
            instantiatedFoodItems.Add(instantiatedItem);                    // Store the instantiated object

        }



        // Instantiate random food items in the remaining spots
        List<int> randFood = generateRandom(1, foodItems.Length, orderFood);

        for (int i = 0; i < foodLocation.Length; i++) // Ensure you're within the bounds of foodLocation
        {
            if (i != orderFood - 1 && randFood.Count > 0) // Ensure it's not the orderFood location
            {
                GameObject randFoodItem = foodDictionary[randFood[0]];
                GameObject instantiatedItem = Instantiate(randFoodItem, foodLocation[i].position, Quaternion.identity);
                randFood.RemoveAt(0); // Remove after instantiation
                instantiatedFoodItems.Add(instantiatedItem); // Store the instantiated object
            }
        }
    }

    // Helper function to clear old instances
    void ClearBoard()
    {
        // Destroy all order items
        foreach (GameObject item in instantiatedOrderItems)
        {
            Destroy(item);
        }
        instantiatedOrderItems.Clear(); // Clear the list

        // Destroy all user chosen items
        foreach (GameObject item in instantiatedUserChosenItems)
        {
            Destroy(item);
        }
        instantiatedUserChosenItems.Clear(); // Clear the list

        // Destroy all random food items
        foreach (GameObject item in instantiatedFoodItems)
        {
            Destroy(item);
        }
        instantiatedFoodItems.Clear(); // Clear the list
    }


}
