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
    private float flashInterval = 0.0166f;       //time between flashes (~30Hz)
    public float gameDuration = 600f;           //Duration of the game: 30 seconds
    private float decodeInterval = 1;        //How often does it decode: 500 ms
    public NeuralCookedRpcClient rpcClient;     //RPC client to communicate with PhysioLabXR
    public int[][] mSequences;                  //m-sequence holder
    public AudioSource audioSource;             //Audio source for the ding for completing an item
    public AudioClip audioClip;                 //Audio clip for the ding for completing an item

    //Game Locations
    public Transform[] orderLocation;           //Where the order will show up
    public Transform[] userChosenLocation;      //Where the user's choices will show up
    public Transform[] foodLocation;            //Where the food will show up

    //Game rules
    public TextMeshPro score;                   //Variable for displaying the score
    public TextMeshPro timer;                   //Variable for displaying the timer
    public int points;                          //Variable for the pints

    //Flashing Cubes
    public GameObject[] cubes;                              //Flashing Cubes
    private Coroutine decodeCoroutine;                      //Creating a coroutine for decoding
    public float[] flashFrequencies;
    private List<Coroutine> flashCoroutines = new List<Coroutine>();

    //Food elements
    public List<GameObject> allFoodItemsPrefabs; // Prefabs for all food items

    private List<string> allFoodItems = new List<string>(); // Names of all food items
    private List<string> orderFoodItems = new List<string>();
    private List<string> userChosenItems = new List<string>();
    private List<GameObject> instantiatedOrderItems = new List<GameObject>();
    private List<GameObject> instantiatedUserChosenItems = new List<GameObject>();
    private List<GameObject> instantiatedFoodItems = new List<GameObject>();

    private List<int> itemNum = new List<int>();
    private string targetItem;
    private List<string> options = new List<string>();

    private Dictionary<string, GameObject> foodDictionary = new Dictionary<string, GameObject>();





    void Start()
    {
        //Initialize all variables
        rpcClient = FindObjectOfType<NeuralCookedRpcClient>();
        flashFrequencies = new float[] { 10f, 14f, 25f };
        points = 0;
        score.text = "Points";
        timer.text = "Timer";
        audioSource = gameObject.gameObject.AddComponent<AudioSource>();
        rpcClient.streamOutlet.push_sample(new float[] { (float)5 });
        //Start the flashing of the cubes
        InvokeRepeating("FlashCubes", 0, flashInterval);  // Calls FlashCubes every 0.033 seconds
        //Start the decoding
        decodeCoroutine = StartCoroutine(DecodeAtIntervals(decodeInterval));
        //Start the game timer
        StartCoroutine(GameTimer(gameDuration));

        foreach (var foodPrefab in allFoodItemsPrefabs)
        {
            allFoodItems.Add(foodPrefab.name);
            foodDictionary[foodPrefab.name] = foodPrefab;
        }

        //Update items
        StartNewOrder();
        ClearBoard();
        //set up board
        UpdateBoard();
    }

    //Game timer function
    IEnumerator GameTimer(float duration)
    {
        float timeRemaining = duration;
        while (timeRemaining > 0)
        {
            //Update the timer display every 0.1 seconds
            timer.text = "Time Left: " + timeRemaining.ToString("0.0") + "s";
            yield return new WaitForSeconds(0.1f);
            timeRemaining -= 0.1f;
        }
        timer.text = "Time Left: 0.0s";

        //Stop the flashing and decoding once time is up
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
        // Start flashing each object with its corresponding frequency
        for (int i = 0; i < cubes.Length; i++)
        {
            if (i < flashFrequencies.Length)
            {
                Coroutine coroutine = StartCoroutine(FlashObjectWithSineWave(cubes[i], flashFrequencies[i]));
                flashCoroutines.Add(coroutine);
            }
        }
    }

    private IEnumerator FlashObjectWithSineWave(GameObject obj, float frequency)
    {
        if (obj == null || frequency <= 0)
            yield break;

        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null)
            yield break;

        float time = 0f;
        Color black = Color.black;
        Color white = Color.white;

        while (true)
        {
            // Calculate sine wave value (-1 to 1)
            float sineValue = Mathf.Sin(2 * Mathf.PI * frequency * time);

            // Normalize sine value to range [0, 1]
            float intensity = (sineValue + 1f) / 2f;

            // Interpolate between black and white
            renderer.material.color = Color.Lerp(black, white, intensity);

            // Increment time by the frame's delta time
            time += Time.deltaTime;

            yield return null; // Wait until the next frame
        }
    }


    //Decoding function
    IEnumerator DecodeAtIntervals(float interval)
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);  //Wait for the specified interval (in seconds)

            if (rpcClient != null)
            {
                // Get decoded Ch
                rpcClient.StartDecode();
                ChosenItem = rpcClient.decoded_choice;

                if (ChosenItem != 0)
                {
                    userChosenItems.Add(ChosenItem);    //add the chosen item to the list
                    Debug.Log($"Decoded choice: {ChosenItem}");
                    UpdateBoard();

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
                            UpdateBoard();
                            //clear userChosenLocation
                        }
                        //if it is not equal, clear the chosen items and do nothing
                        else
                        {
                            userChosenItems.Clear();
                            yield return new WaitForSeconds(0.02f);
                            UpdateBoard();
                        }
                    }

                    //updating the score
                    score.text = points.ToString();
                }
            }
            else { Debug.LogError("rpcClient.Instance is null."); }

        }
    }

    //Method to check if two lists are equal
    bool AreListsEqual(List<int> list1, List<int> list2)
    {
        if (list1.Count != list2.Count) return false;

        for (int i = 0; i < list1.Count; i++)
        {
            if (list1[i] != list2[i]) return false;
        }
        return true;
    }

    void StartNewOrder()
    {
        orderFoodItems.Clear();
        userChosenItems.Clear();
        itemNum.Clear();
        options.Clear();

        AssignRandomNumbersToItems();
        GenerateCustomerOrder();
        ProcessNextItem();
        UpdateBoard();
    }

    void AssignRandomNumbersToItems()
    {
        itemNum.Clear();
        for (int i = 0; i < allFoodItems.Count; i++)
        {
            itemNum.Add(i);
        }

        // Shuffle itemNum for random assignment
        for (int i = itemNum.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            int temp = itemNum[i];
            itemNum[i] = itemNum[randomIndex];
            itemNum[randomIndex] = temp;
        }
    }

    void GenerateCustomerOrder()
    {
        for (int i = 0; i < 3; i++) // First 3 items in the list are the customer's order
        {
            orderFoodItems.Add(allFoodItems[itemNum[i]]);
        }
    }

    void ProcessNextItem()
    {
        if (userChosenItems.Count == orderFoodItems.Count)
        {
            AreListsEqual();
            return;
        }

        int nextItemIndex = userChosenItems.Count;
        targetItem = orderFoodItems[nextItemIndex];
        GenerateOptions(nextItemIndex);
        UpdateBoard();
    }

    void GenerateOptions(int targetIndex)
    {
        options.Clear();
        options.Add(orderFoodItems[targetIndex]);

        while (options.Count < 3) // Ensure exactly 3 options
        {
            string randomItem = allFoodItems[UnityEngine.Random.Range(0, allFoodItems.Count)];

            if (!orderFoodItems.Contains(randomItem) && !options.Contains(randomItem))
            {
                options.Add(randomItem);
            }
        }

        // Shuffle options for randomness
        for (int i = options.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            string temp = options[i];
            options[i] = options[randomIndex];
            options[randomIndex] = temp;
        }
    }

    void UpdateBoard()
    {
        ClearBoard();

        // Spawn the ordered food based on orderFoodItems
        int foodIndex = 0;
        foreach (string item in orderFoodItems)
        {
            GameObject foodItem = foodDictionary[item];
            GameObject instantiatedItem = Instantiate(foodItem, orderLocation[foodIndex].position, Quaternion.identity);
            instantiatedOrderItems.Add(instantiatedItem); // Store the instantiated object
            foodIndex++;
        }

        // Instantiate the user's chosen food items
        int userIndex = 0;
        foreach (string item in userChosenItems)
        {
            GameObject foodItem = foodDictionary[item];
            GameObject instantiatedItem = Instantiate(foodItem, userChosenLocation[userIndex].position, Quaternion.identity);
            instantiatedUserChosenItems.Add(instantiatedItem); // Store the instantiated object
            userIndex++;
        }

        // Instantiate the options
        int optionsIndex = 0;
        foreach (string item in options)
        {
            GameObject foodItem = foodDictionary[item];
            GameObject instantiatedItem = Instantiate(foodItem, foodLocation[optionsIndex].position, Quaternion.identity);
            instantiatedFoodItems.Add(instantiatedItem); // Store the instantiated object
            optionsIndex++;
        }
    }

    void ClearBoard()
    {
        // Destroy all order items
        foreach (GameObject item in instantiatedOrderItems)
        {
            Destroy(item);
        }
        instantiatedOrderItems.Clear();

        // Destroy all user chosen items
        foreach (GameObject item in instantiatedUserChosenItems)
        {
            Destroy(item);
        }
        instantiatedUserChosenItems.Clear();

        // Destroy all option items
        foreach (GameObject item in instantiatedFoodItems)
        {
            Destroy(item);
        }
        instantiatedFoodItems.Clear();
    }
}
