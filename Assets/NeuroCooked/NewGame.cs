using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;

public class MainGame : MonoBehaviour
{
    // Locations of where food sprites will populate 
    public Transform[] orderLoc; // Where the order will show up
    public Transform[] chosenLoc; // Where the user's choices will show up
    public Transform[] optionsLoc; // Where the food will show up

    // Game Constants
    public float gameDuration = 600f; // Duration of the game: 600 seconds
    public NeuralCookedRpcClient rpcClient; // RPC client to communicate with PhysioLabXR


    // UI Elements
    public TextMeshPro scoreText;
    public TextMeshPro timerText;
    private int points;
    // Add audio element

    // Variables important for decoding EEG signals
    public GameObject[] cubes; // Flashing Cubes
    public float[] flashFrequencies; // Frequencies the cubes will be flashing at for SSVEP
    public float[] mSequences; // Storing the m-sequences
    private Coroutine decodeCoroutine; // Function called when decode interval is met
    private float decodeInterval = 1f; // Decoding interval: 1 second
    private float flashInterval = 0.0166f; // Time between flashes (~30Hz)
    private List<Coroutine> flashCoroutines = new List<Coroutine>();
    private int chosenItem = 0; // The item the user has chosen

    // Food Elements
    private Dictionary<string, GameObject> foodDictionary = new Dictionary<string, GameObject>();
    private Dictionary<string, int> orderNumbered = new Dictionary<string, int>();
    private string[] chosenFoods = new string [3];
    private string[] optionFood;
    private int currentTarget = 0;
    private List<GameObject> instantiatedOrderItems = new List<GameObject>();
    private List<GameObject> instantiatedChosenItems = new List<GameObject>();
    private List<GameObject> instantiatedOptionsItems = new List<GameObject>();
    void Start()
    {
        // Initialize game variables
        // rpcClient = FindObjectOfType<NeuralCookedRpcClient>();
        flashFrequencies = new float[] { 10f, 14f, 25f };
        points = 0;
        scoreText.text = "Points: 0";
        timerText.text = "Time Left: " + gameDuration + "s";
        loadModels();

        // Start the game
        //InvokeRepeating("FlashCubes", 0, flashInterval);
        decodeCoroutine = StartCoroutine(DecodeAtIntervals(decodeInterval));
        StartCoroutine(GameTimer(gameDuration));
        StartNewOrder();
        UpdateBoard();  
    }
    void loadModels()
    {
        List<string> modelNames = new List<string> {"Burger", "Coffee", "Croissant", "Fries", "HotDog", "IceCream", "Juice", "Taco" };
        foreach (string modelName in modelNames) {
            GameObject model = Resources.Load<GameObject>("Models/" + modelName);
            if (model != null)
            {
                foodDictionary[modelName] = model;
            }
            else
            {
                Debug.LogWarning($"Model for {modelName} not found in Resources/Models!");
            }
        }

    }
    IEnumerator GameTimer(float duration)
    {
        float timeRemaining = duration;
        while (timeRemaining > 0)
        {
            timerText.text = "Time Left: " + timeRemaining.ToString("0.0") + "s";
            yield return new WaitForSeconds(0.1f);
            timeRemaining -= 0.1f;
        }
        timerText.text = "Time Left: 0.0s";

        CancelInvoke("FlashCubes");
        if (decodeCoroutine != null)
        {
            StopCoroutine(decodeCoroutine);
        }
        timerText.text = "Game Ended.";
    }

    void FlashCubes()
    {
        for (int i = 0; i<cubes.Length; i++)
        {
            if (i<flashFrequencies.Length)
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
            float sineValue = Mathf.Sin(2 * Mathf.PI * frequency * time);
            float intensity = (sineValue + 1f) / 2f;
            renderer.material.color = Color.Lerp(black, white, intensity);
            time += Time.deltaTime;
            yield return null;
        }
    }

    void Update()
    {

        if (rpcClient == null) // Only check input if rpcClient is null
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                chosenItem = 1;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                chosenItem = 2;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                chosenItem = 3;
            }
        }
    }

    IEnumerator DecodeAtIntervals(float interval)
    {
        while (true)
        { 
            yield return new WaitForSeconds(interval);

            if (rpcClient != null)
            {
                rpcClient.StartDecode();
                chosenItem = rpcClient.decoded_choice;
            }

            if (chosenItem != 0)
            {
                chosenFoods[currentTarget] = optionFood[chosenItem - 1];
                Debug.Log($"Chosen Food: {chosenFoods[currentTarget]}");
                chosenItem = 0;
                if (currentTarget == 2)
                {
                    // Check if the user's choices match the order
                    bool exactMatch = chosenFoods.SequenceEqual(orderNumbered.Keys);
                    Debug.Log($"Exact Match: {exactMatch}");
                    if (exactMatch)
                    {
                        points++;
                        StartNewOrder();
                        scoreText.text = "Points: " + points;
                    }
                    Debug.Log($"Points: {points}");
                    currentTarget = 0;
                    chosenFoods = new string[3];
                    createOptions(currentTarget);
                    UpdateBoard();
                }
                else
                {
                    currentTarget++;
                    createOptions(currentTarget);
                    UpdateBoard();
                }

            }
        }
    }
    void StartNewOrder()
        {
            // Randomly select 3 items from the food dictionary and give them numbers from 1-3 randomly
            List<string> orderItems = foodDictionary.Keys.OrderBy(x => UnityEngine.Random.value).Take(3).ToList();
            orderNumbered = new Dictionary<string, int>();

            foreach (string item in orderItems)
            {
                int number = UnityEngine.Random.Range(1, 4);
                orderNumbered[item] = number;
            }

            // Created a dictionary of the ordered food items and the cooresponding cube it should be placed in.
            // Populate the orderLoc with the first ordered food and the other two options
            createOptions(currentTarget);
        }

    void createOptions(int currentNum)
    {
        KeyValuePair<string, int> currentTarget = orderNumbered.ElementAt(currentNum);

        var otherOptions = foodDictionary
            .Where(pair => pair.Key != currentTarget.Key)
            .ToList().OrderBy(_ => UnityEngine.Random.value)
            .Take(2).ToList();
        optionFood = new string[3];
        optionFood[currentTarget.Value - 1] = currentTarget.Key;
        int index = 0;
        for (int i = 0; i < 3; i++)
        {
            if (i != currentTarget.Value-1)
            {
                optionFood[i] = otherOptions[index].Key;
                index++;
            }
        }
        for (int i = 0; i < optionFood.Length; i++)
        {
            string value = string.IsNullOrEmpty(optionFood[i]) ? "Empty" : optionFood[i];
        }
    }
    void UpdateBoard()
    {
        ClearBoard();
        //foreach (string item in chosenFoods)
        //{
        //    Debug.Log($"Chosen Food Item: {item}");
        //}

        //foreach (var key in orderNumbered.Keys)
        //{
        //    Debug.Log($"orderNumbered Key: '{key}'");
        //}

        if (foodDictionary == null)
        {
            Debug.LogError("Food Dictionary is null!");
            return;
        }
        int foodIndex = 0;
        foreach (string item in orderNumbered.Keys)
        {
            GameObject foodItem = foodDictionary[item];
            GameObject instantiatedItem = Instantiate(foodItem, orderLoc[foodIndex].position, Quaternion.identity);
            instantiatedItem.transform.localScale = orderLoc[foodIndex].localScale;
            instantiatedOrderItems.Add(instantiatedItem);
            foodIndex++;
        }

        int userIndex = 0;
        foreach (string item in chosenFoods)
        {
            if ( item != null)
            {
                GameObject foodItem = foodDictionary[item];
                GameObject instantiatedItem = Instantiate(foodItem, chosenLoc[userIndex].position, Quaternion.identity);
                instantiatedItem.transform.localScale = chosenLoc[userIndex].localScale;
                instantiatedChosenItems.Add(instantiatedItem);
                userIndex++;
            }
            else
            {
                continue;
            }

        }
        int optionsIndex = 0;
        foreach (string item in optionFood)
        {
            GameObject foodItem = foodDictionary[item];
            GameObject instantiatedItem = Instantiate(foodItem, optionsLoc[optionsIndex].position, Quaternion.identity);
            instantiatedItem.transform.localScale = optionsLoc[optionsIndex].localScale;
            instantiatedOptionsItems.Add(instantiatedItem);
            optionsIndex++;
        }
    }

    void ClearBoard()
    {
        foreach (GameObject item in instantiatedOrderItems)
        {
            Destroy(item);
        }
        instantiatedOrderItems.Clear();

        foreach (GameObject item in instantiatedChosenItems)
        {
            Destroy(item);
        }
        instantiatedChosenItems.Clear();

        foreach (GameObject item in instantiatedOptionsItems)
        {
            Destroy(item);
        }
        instantiatedOptionsItems.Clear();
    }
}
