using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class keyListener : MonoBehaviour
{

    public Timer timer;
    // Start is called before the first frame update
    void Start()
    {
     
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Detected key code: " + KeyCode.Space);
            timer.buttonPress();
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            Debug.Log("Detected key code: " + KeyCode.Q);
            Application.Quit();
        }
    }
}
