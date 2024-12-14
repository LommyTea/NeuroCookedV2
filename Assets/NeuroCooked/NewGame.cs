using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;

public class MainGame : MonoBehaviour
{
    // Game Constants
    private float flashInterval = 0.0166f; // Time between flashes (~30Hz)
    public float gameDuration = 600f; // Duration of the game: 600 seconds
    private float decodeInterval = 1f; // Decoding interval: 1 second
    public NeuralCookedRpcClient rpcClient; // RPC client to communicate with PhysioLabXR
    public AudioSource audioSource; // Audio source for the ding
    public AudioClip audioClip; // Audio clip for the ding

    // Game Locations
    public Transform[] orderLocation; // Where the order will show up
    public Transform[] userChosenLocation; // Where the user's choices will show up
    public Transform[] foodLocation; // Where the food will show up

    // UI Elements
    public TextMeshPro scoreText;
    public TextMeshPro timerText;
    private int points;

    // Flashing Cubes
    public GameObject[] cubes; // Flashing Cubes
    private Coroutine decodeCoroutine;
    public float[] flashFrequencies;
    private List<Coroutine> flashCoroutines = new List<Coroutine>();

    // Food Elements
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
        // Initialize game variables
        rpcClient = FindObjectOfType<NeuralCookedRpcClient>();
        flashFrequencies = new float[] { 10f, 14f, 25f };
        points = 0;
        scoreText.text = "Points: 0";
        timerText.text = "Time Left: " + gameDuration + "s";
        audioSource = gameObject.AddComponent<AudioSource>();

        foreach (var foodPrefab in allFoodItemsPrefabs)
        {
            allFoodItems.Add(foodPrefab.name);
            foodDictionary[foodPrefab.name] = foodPrefab;
        }

        // Start the game
        InvokeRepeating("FlashCubes", 0, flashInterval);
        decodeCoroutine = StartCoroutine(DecodeAtIntervals(decodeInterval));
        StartCoroutine(GameTimer(gameDuration));
        StartNewOrder();
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
            float sineValue = Mathf.Sin(2 * Mathf.PI * frequency * time);
            float intensity = (sineValue + 1f) / 2f;
            renderer.material.color = Color.Lerp(black, white, intensity);
            time += Time.deltaTime;
            yield return null;
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
                int chosenItem = rpcClient.decoded_choice;

                if (chosenItem != 0)
                {
                    string itemName = allFoodItems[chosenItem - 1];
                    userChosenItems.Add(itemName);
                    Debug.Log($"Decoded choice: {itemName}");
                    GenerateOptions();
                    UpdateBoard();

                    if (userChosenItems.Count == orderFoodItems.Count)
                    {
                        if (AreListsEqual(orderFoodItems, userChosenItems))
                        {
                            points++;
                            audioSource.PlayOneShot(audioClip);
                            Debug.Log("Order matched successfully!");
                        }
                        else
                        {
                            Debug.Log("Order did not match.");
                        }

                        userChosenItems.Clear();
                        StartNewOrder();
                    }

                    scoreText.text = "Points: " + points;
                }
            }
            else
            {
                Debug.LogError("rpcClient.Instance is null.");
            }
        }
    }

    bool AreListsEqual(List<string> list1, List<string> list2)
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
        options.Clear();
        AssignRandomNumbersToItems();
        GenerateCustomerOrder();
        GenerateOptions();
        UpdateBoard();
    }

    void AssignRandomNumbersToItems()
    {
        itemNum.Clear();
        for (int i = 0; i < allFoodItems.Count; i++)
        {
            itemNum.Add(i);
        }

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
        for (int i = 0; i < 3; i++)
        {
            orderFoodItems.Add(allFoodItems[itemNum[i]]);
        }
    }

    void GenerateOptions()
    {
        options.Clear();

        // Ensure the next required item is added first
        if (userChosenItems.Count < orderFoodItems.Count)
        {
            string nextRequiredItem = orderFoodItems[userChosenItems.Count];
            options.Add(nextRequiredItem);
        }

        // Fill the remaining options with random items (excluding duplicates)
        List<string> remainingItems = allFoodItems.Except(options).ToList();
        ShuffleList(remainingItems);

        while (options.Count < 3)
        {
            options.Add(remainingItems[0]);
            remainingItems.RemoveAt(0);
        }

        // Shuffle options to randomize the order
        ShuffleList(options);

        // Debug logs to track state
        Debug.Log($"Options generated: {string.Join(", ", options)}");
        Debug.Log($"Food locations available: {foodLocation.Length}");
    }

    // Helper method to shuffle a list
    void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }



    void UpdateBoard()
    {
        ClearBoard();

        int foodIndex = 0;
        foreach (string item in orderFoodItems)
        {
            GameObject foodItem = foodDictionary[item];
            GameObject instantiatedItem = Instantiate(foodItem, orderLocation[foodIndex].position, Quaternion.identity);
            instantiatedOrderItems.Add(instantiatedItem);
            foodIndex++;
        }

        int userIndex = 0;
        foreach (string item in userChosenItems)
        {
            GameObject foodItem = foodDictionary[item];
            GameObject instantiatedItem = Instantiate(foodItem, userChosenLocation[userIndex].position, Quaternion.identity);
            instantiatedUserChosenItems.Add(instantiatedItem);
            userIndex++;
        }
        // Add debug logs
        Debug.Log($"Options count: {options.Count}");
        Debug.Log($"FoodLocation count: {foodLocation.Length}");
        int optionsIndex = 0;
        foreach (string item in options)
        {
            Debug.Log(options);
            GameObject foodItem = foodDictionary[item];
            GameObject instantiatedItem = Instantiate(foodItem, foodLocation[optionsIndex].position, Quaternion.identity);
            instantiatedFoodItems.Add(instantiatedItem);
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

        foreach (GameObject item in instantiatedUserChosenItems)
        {
            Destroy(item);
        }
        instantiatedUserChosenItems.Clear();

        foreach (GameObject item in instantiatedFoodItems)
        {
            Destroy(item);
        }
        instantiatedFoodItems.Clear();
    }
}
